using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CaracterizandoAtividadeCodeReview
{
  class Program
  {
    private static readonly string GitHubToken = "";
    private static readonly HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
      client.DefaultRequestHeaders.Add("User-Agent", "GitHubGraphQLClient");
      client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");

      // Busca até 200 repositórios com ≥100 PRs
      var repositorios = await GetPopularRepositories(200);
      Console.WriteLine($"Selecionados {repositorios.Count} repositórios.");

      if (repositorios.Count == 0)
      {
        Console.WriteLine("Nenhum repositório encontrado. Verifique o token ou a API.");
        return;
      }

      // Exemplo: pegar os PRs do primeiro repo
      var primeiroRepo = repositorios[0];
      Console.WriteLine($"Coletando PRs do repositório: {primeiroRepo.Owner}/{primeiroRepo.Name}");

      var prs = await GetPullRequests(primeiroRepo.Owner, primeiroRepo.Name, maxPages: 3);

      SaveToCsv("pull_requests.csv", prs);
      Console.WriteLine("Arquivo CSV gerado com sucesso!");
    }

    // -------------------------------
    // Busca repositórios populares
    // -------------------------------
    public static async Task<List<RepositoryData>> GetPopularRepositories(int reposMax = 200)
    {
      var reposList = new List<RepositoryData>();
      string? cursor = null;
      bool hasNextPage = true;

      string query = @"
                query($cursor:String) {
                  search(query: ""stars:>100 sort:stars"", type: REPOSITORY, first: 100, after: $cursor) {
                    pageInfo {
                      hasNextPage
                      endCursor
                    }
                    nodes {
                      ... on Repository {
                        name
                        owner { login }
                        pullRequests { totalCount }
                      }
                    }
                  }
                }";

      while (hasNextPage && reposList.Count < reposMax)
      {
        var variables = new { cursor = cursor };
        var requestBody = new { query = query, variables = variables };
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.github.com/graphql", requestContent);
        string json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verifica erros da API
        if (root.TryGetProperty("errors", out var errors))
        {
          Console.WriteLine("Erro da API: " + errors.ToString());
          return reposList;
        }

        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("search", out var search))
        {
          Console.WriteLine("Não veio 'data.search' na resposta");
          return reposList;
        }

        var nodes = search.GetProperty("nodes");

        foreach (var node in nodes.EnumerateArray())
        {
          int prCount = node.GetProperty("pullRequests").GetProperty("totalCount").GetInt32();
          if (prCount >= 100)
          {
            reposList.Add(new RepositoryData
            {
              Owner = node.GetProperty("owner").GetProperty("login").GetString() ?? "",
              Name = node.GetProperty("name").GetString() ?? "",
              PullRequestCount = prCount
            });

            if (reposList.Count >= reposMax)
              break;
          }
        }

        var pageInfo = search.GetProperty("pageInfo");
        hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
        cursor = pageInfo.GetProperty("endCursor").GetString();
      }

      return reposList;
    }

    // -------------------------------
    // Busca PRs de um repositório
    // -------------------------------
    public static async Task<List<PullRequestData>> GetPullRequests(string owner, string name, int maxPages = 5)
    {
      var prList = new List<PullRequestData>();
      string? cursor = null;

      string query = @"
                query($owner:String!, $name:String!, $cursor:String) {
                  repository(owner:$owner, name:$name) {
                    pullRequests(first: 50, after: $cursor, states: [MERGED, CLOSED]) {
                      pageInfo {
                        hasNextPage
                        endCursor
                      }
                      nodes {
                        number
                        title
                        bodyText
                        createdAt
                        closedAt
                        additions
                        deletions
                        changedFiles
                        reviews { totalCount }
                        comments { totalCount }
                        participants { totalCount }
                      }
                    }
                  }
                }";

      for (int i = 0; i < maxPages; i++)
      {
        var variables = new { owner = owner, name = name, cursor = cursor };
        var requestBody = new { query = query, variables = variables };
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.github.com/graphql", requestContent);
        string json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors))
        {
          Console.WriteLine("Erro: " + errors.ToString());
          break;
        }

        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("repository", out var repoNode))
        {
          Console.WriteLine("Não veio 'data.repository' na resposta");
          break;
        }

        if (!repoNode.TryGetProperty("pullRequests", out var pullRequests))
          break;

        foreach (var pr in pullRequests.GetProperty("nodes").EnumerateArray())
        {
          int reviews = pr.GetProperty("reviews").GetProperty("totalCount").GetInt32();
          DateTime created = pr.GetProperty("createdAt").GetDateTime();
          DateTime closed = pr.GetProperty("closedAt").GetDateTime();
          double reviewTime = (closed - created).TotalHours;

          if (reviewTime >= 1)
          {
            prList.Add(new PullRequestData
            {
              Number = pr.GetProperty("number").GetInt32(),
              Title = pr.GetProperty("title").GetString() ?? "",
              BodyLength = pr.GetProperty("bodyText").GetString()?.Length ?? 0,
              CreatedAt = created,
              ClosedAt = closed,
              ReviewTimeHours = reviewTime,
              Additions = pr.GetProperty("additions").GetInt32(),
              Deletions = pr.GetProperty("deletions").GetInt32(),
              ChangedFiles = pr.GetProperty("changedFiles").GetInt32(),
              Reviews = reviews,
              Comments = pr.GetProperty("comments").GetProperty("totalCount").GetInt32(),
              Participants = pr.GetProperty("participants").GetProperty("totalCount").GetInt32()
            });
          }
        }

        var pageInfo = pullRequests.GetProperty("pageInfo");
        bool hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
        if (!hasNextPage) break;
        cursor = pageInfo.GetProperty("endCursor").GetString();
      }

      return prList;
    }

    // -------------------------------
    // Salvar em CSV
    // -------------------------------
    public static void SaveToCsv(string filePath, List<PullRequestData> prs)
    {
      using var writer = new StreamWriter(filePath);
      writer.WriteLine("Number,Title,BodyLength,CreatedAt,ClosedAt,ReviewTimeHours,Additions,Deletions,ChangedFiles,Reviews,Comments,Participants");

      foreach (var pr in prs)
      {
        writer.WriteLine($"{pr.Number},\"{pr.Title.Replace("\"", "'")}\",{pr.BodyLength},{pr.CreatedAt},{pr.ClosedAt},{pr.ReviewTimeHours},{pr.Additions},{pr.Deletions},{pr.ChangedFiles},{pr.Reviews},{pr.Comments},{pr.Participants}");
      }
    }
  }

  // -------------------------------
  // Modelos de dados
  // -------------------------------
  public class PullRequestData
  {
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public int BodyLength { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ClosedAt { get; set; }
    public double ReviewTimeHours { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int ChangedFiles { get; set; }
    public int Reviews { get; set; }
    public int Comments { get; set; }
    public int Participants { get; set; }
  }

  public class RepositoryData
  {
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public int PullRequestCount { get; set; }
  }
}
