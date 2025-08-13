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

    while (hasNextPage && totalFetched < 100)
    {
      string query = @$"
      {{
        search(query: ""stars:>0 sort:stars-desc"", type: REPOSITORY, first: 10{(cursor != null ? $@", after: ""{cursor}""" : "")}) {{
          pageInfo {{
            hasNextPage
            endCursor
          }}
          nodes {{
            ... on Repository {{
              name
              description
              url
              stargazerCount
              forkCount
              updatedAt
              createdAt
              primaryLanguage {{
                name
              }}
              issues {{
                totalCount
              }}
              closedIssues: issues(states: CLOSED) {{
                totalCount
              }}
              pullRequests {{
                totalCount
              }}
              releases {{
                totalCount
              }}
            }}
          }}
        }}
      }}";

      var requestBody = new { query };
      var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
      HttpResponseMessage response;
      try
      {
        response = await httpClient.PostAsync(endpoint, jsonContent);
        response.EnsureSuccessStatusCode();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"erro: {ex.Message}");
        return;
      }

      var responseBody = await response.Content.ReadAsStringAsync();
      using var jsonDoc = JsonDocument.Parse(responseBody);
      var root = jsonDoc.RootElement;

      if (root.TryGetProperty("errors", out var errors))
      {
        Console.WriteLine("erro na API:");
        Console.WriteLine(errors.ToString());
        return;
      }

      if (!root.TryGetProperty("data", out var dataElement))
      {
        Console.WriteLine(responseBody);
        return;
      }

      var searchElement = dataElement.GetProperty("search");
      hasNextPage = searchElement.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean();
      cursor = searchElement.GetProperty("pageInfo").GetProperty("endCursor").GetString();

      var nodes = searchElement.GetProperty("nodes").EnumerateArray();
      Console.WriteLine($"Página carregada: {searchElement.GetProperty("nodes").GetArrayLength()} repositórios");

      foreach (var node in nodes)
      {
        allRepos.Add(new Repository
        {
          Name = node.GetProperty("name").GetString() ?? "",
          Description = node.GetProperty("description").GetString() ?? "",
          Url = node.GetProperty("url").GetString() ?? "",
          Stars = node.GetProperty("stargazerCount").GetInt32(),
          Forks = node.GetProperty("forkCount").GetInt32(),
          UpdatedAt = node.GetProperty("updatedAt").GetDateTime(),
          CreatedAt = node.GetProperty("createdAt").GetDateTime(),
          PrimaryLanguage = node.TryGetProperty("primaryLanguage", out var lang) && lang.ValueKind == JsonValueKind.Object && lang.TryGetProperty("name", out var langName) ? langName.GetString() ?? "" : "",
          IssuesTotal = node.GetProperty("issues").GetProperty("totalCount").GetInt32(),
          IssuesClosed = node.GetProperty("closedIssues").GetProperty("totalCount").GetInt32(),
          PullRequests = node.GetProperty("pullRequests").GetProperty("totalCount").GetInt32(),
          Releases = node.GetProperty("releases").GetProperty("totalCount").GetInt32()
        });
      }

      totalFetched = allRepos.Count;
      Console.WriteLine($"Total acumulado: {totalFetched}");
    }

    Console.WriteLine($"repositorios obtidos: {allRepos.Count}");

    var csvLines = new List<string>
    {
      "Nome,Descrição,URL,Stars,Forks,Criado Em,Última Atualização,Linguagem,Issues Totais,Issues Fechadas,PRs,Releases"
    };

    foreach (var repo in allRepos)
    {
      string descEscaped = repo.Description?.Replace("\"", "\"\"") ?? "";
      string nameEscaped = repo.Name?.Replace("\"", "\"\"") ?? "";
      string updatedAtStr = repo.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
      string createdAtStr = repo.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

      csvLines.Add($"\"{nameEscaped}\",\"{descEscaped}\",{repo.Url},{repo.Stars},{repo.Forks},{createdAtStr},{updatedAtStr},\"{repo.PrimaryLanguage}\",{repo.IssuesTotal},{repo.IssuesClosed},{repo.PullRequests},{repo.Releases}");
    }

    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "repositorios_populares.csv");
    await File.WriteAllLinesAsync(filePath, csvLines, Encoding.UTF8);

    Console.WriteLine($"finalizado");
  }

  class Repository
  {
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public int Stars { get; set; }
    public int Forks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int PullRequests { get; set; }
    public int Releases { get; set; }
    public int IssuesTotal { get; set; }
    public int IssuesClosed { get; set; }
    public string? PrimaryLanguage { get; set; }
  }
}
