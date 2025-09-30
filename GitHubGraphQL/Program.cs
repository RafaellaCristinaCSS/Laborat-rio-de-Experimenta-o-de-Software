using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

class Program
{
  static async Task Main(string[] args)
  {
    string githubToken = "";
    string endpoint = "https://api.github.com/graphql";
    var allRepos = new List<Repository>();
    string? cursor = null;
    bool hasNextPage = true;
    int totalFetched = 0;

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubApiApp", "1.0"));
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);

    while (hasNextPage && totalFetched < 1000)
    {
      string query = @$"
            {{
              search(query: ""language:Java sort:stars"", type: REPOSITORY, first: 20, after: {FormatCursor(cursor)}) {{
                pageInfo {{
                  hasNextPage
                  endCursor
                }}
                nodes {{
                  ... on Repository {{
                    name
                    nameWithOwner
                    description
                    url
                    stargazerCount
                    forkCount
                    createdAt
                    updatedAt
                    primaryLanguage {{
                      name
                    }}
                    pullRequests(states: MERGED) {{
                      totalCount
                    }}
                    issues {{
                      totalCount
                    }}
                    issuesClosed: issues(states: CLOSED) {{
                      totalCount
                    }}
                    releases {{
                      totalCount
                    }}
                    defaultBranchRef {{
                      target {{
                        ... on Commit {{
                          history {{
                            totalCount
                          }}
                        }}
                      }}
                    }}
                  }}
                }}
              }}
            }}";

      var jsonDoc = await RunQuery(httpClient, endpoint, query);
      if (jsonDoc == null) return;

      var data = jsonDoc.RootElement.GetProperty("data").GetProperty("search");

      hasNextPage = data.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean();
      cursor = data.GetProperty("pageInfo").GetProperty("endCursor").GetString();

      var nodes = data.GetProperty("nodes").EnumerateArray();
      Console.WriteLine($"Página carregada: {nodes.Count()} repositórios");

      foreach (var node in nodes)
      {
        int commits = 0;
        try
        {
          commits = node
              .GetProperty("defaultBranchRef")
              .GetProperty("target")
              .GetProperty("history")
              .GetProperty("totalCount")
              .GetInt32();
        }
        catch
        {
          // se não tiver branch padrão ou history
        }

        allRepos.Add(new Repository
        {
          Name = node.GetProperty("name").GetString() ?? "",
          NameWithOwner = node.GetProperty("nameWithOwner").GetString() ?? "",
          Description = node.GetProperty("description").GetString() ?? "",
          Url = node.GetProperty("url").GetString() ?? "",
          Stars = node.GetProperty("stargazerCount").GetInt32(),
          Forks = node.GetProperty("forkCount").GetInt32(),
          CreatedAt = node.GetProperty("createdAt").GetDateTime(),
          UpdatedAt = node.GetProperty("updatedAt").GetDateTime(),
          PrimaryLanguage = node.TryGetProperty("primaryLanguage", out var lang) &&
                              lang.ValueKind == JsonValueKind.Object &&
                              lang.TryGetProperty("name", out var langName)
                                  ? langName.GetString() ?? ""
                                  : "",
          IssuesTotal = node.GetProperty("issues").GetProperty("totalCount").GetInt32(),
          IssuesClosed = node.GetProperty("issuesClosed").GetProperty("totalCount").GetInt32(),
          PullRequests = node.GetProperty("pullRequests").GetProperty("totalCount").GetInt32(),
          Releases = node.GetProperty("releases").GetProperty("totalCount").GetInt32(),
          Commits = commits
        });
      }

      totalFetched = allRepos.Count;
      Console.WriteLine($"Total acumulado: {totalFetched}");
    }

    Console.WriteLine($"Repositorios obtidos: {allRepos.Count}");

    // agora ordenamos pelos mais engajados
    var top20 = allRepos
        .OrderByDescending(r => r.EngagementScore)
        .Take(20)
        .ToList();

    var csvLines = new List<string>
        {
            "Nome,NomeCompleto,Descrição,URL,Stars,Forks,Criado Em,Última Atualização,Linguagem,Issues Totais,Issues Fechadas,PRs,Releases,Commits,Engajamento"
        };

    foreach (var repo in top20)
    {
      string descEscaped = repo.Description?.Replace("\"", "\"\"") ?? "";
      string nameEscaped = repo.Name?.Replace("\"", "\"\"") ?? "";
      string updatedAtStr = repo.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
      string createdAtStr = repo.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

      csvLines.Add(
          $"\"{nameEscaped}\",\"{repo.NameWithOwner}\",\"{descEscaped}\",{repo.Url},{repo.Stars},{repo.Forks},{createdAtStr},{updatedAtStr},\"{repo.PrimaryLanguage}\",{repo.IssuesTotal},{repo.IssuesClosed},{repo.PullRequests},{repo.Releases},{repo.Commits},{repo.EngagementScore}"
      );
    }

    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "repositoriosJavaTop20.csv");
    await File.WriteAllLinesAsync(filePath, csvLines, Encoding.UTF8);

    Console.WriteLine("Finalizado");
  }

  static async Task<JsonDocument?> RunQuery(HttpClient client, string endpoint, string query, int maxRetries = 3)
  {
    var requestBody = new { query };
    var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
      try
      {
        var response = await client.PostAsync(endpoint, jsonContent);

        if (response.IsSuccessStatusCode)
        {
          var responseBody = await response.Content.ReadAsStringAsync();
          var jsonDoc = JsonDocument.Parse(responseBody);

          if (jsonDoc.RootElement.TryGetProperty("errors", out var errors))
          {
            Console.WriteLine("Erro na API:");
            Console.WriteLine(errors.ToString());
            return null;
          }

          return jsonDoc;
        }

        // Se for 502 (Bad Gateway), tenta novamente com backoff
        if ((int)response.StatusCode == 502)
        {
          Console.WriteLine($"Erro 502 (Bad Gateway), tentativa {attempt}/{maxRetries}");
          if (attempt < maxRetries)
          {
            await Task.Delay(2000 * attempt); // backoff exponencial simples
            continue;
          }
        }

        response.EnsureSuccessStatusCode(); // lança exceção para outros códigos
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Erro na tentativa {attempt}: {ex.Message}");
        if (attempt < maxRetries)
        {
          await Task.Delay(2000 * attempt);
          continue;
        }
        return null;
      }
    }

    return null; // se todas as tentativas falharem
  }
  static string FormatCursor(string? cursor)
  {
    return cursor == null ? "null" : $"\"{cursor}\"";
  }


  class Repository
  {
    public string? Name { get; set; }
    public string? NameWithOwner { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public int Stars { get; set; }
    public int Forks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? PrimaryLanguage { get; set; }
    public int PullRequests { get; set; }
    public int IssuesTotal { get; set; }
    public int IssuesClosed { get; set; }
    public int Releases { get; set; }
    public int Commits { get; set; }
    public int EngagementScore => Commits + PullRequests + IssuesClosed; /// Calcula uma métrica de engajamento simples somando commits, PRs e issues fechadas.
  }

}
