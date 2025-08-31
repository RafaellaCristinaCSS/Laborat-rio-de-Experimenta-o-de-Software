
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Programa para buscar repositórios populares do GitHub usando a API GraphQL,
/// extrair informações relevantes e salvar em um arquivo CSV.
/// </summary>

/// <summary>
/// Classe principal do programa.
/// </summary>
class Program
{
  /// <summary>
  /// Ponto de entrada do programa. Realiza a busca dos repositórios, coleta os dados e gera o CSV.
  /// </summary>
  /// <param name="args">Argumentos de linha de comando (não utilizados).</param>
  static async Task Main(string[] args)
  {

    // Token de autenticação do GitHub. Deve ser preenchido pelo usuário.
    string githubToken = "";
    // Endpoint da API GraphQL do GitHub.
    string endpoint = "https://api.github.com/graphql";
    // Lista para armazenar todos os repositórios coletados.
    var allRepos = new List<Repository>();
    // Cursor para paginação da API.
    string? cursor = null;
    // Indica se há mais páginas de resultados.
    bool hasNextPage = true;
    // Contador de repositórios coletados.
    int totalFetched = 0;

    // Configuração do cliente HTTP para requisições à API do GitHub.
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubApiApp", "1.0"));
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);

    // Loop para buscar repositórios em páginas, até atingir o limite ou acabar os resultados.
    while (hasNextPage && totalFetched < 1000)
    {

      // Monta a query GraphQL para buscar repositórios populares.
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


      // Prepara o corpo da requisição e envia para a API.
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
        // Exibe erro de conexão ou autenticação.
        Console.WriteLine($"erro: {ex.Message}");
        return;
      }


      // Lê e interpreta a resposta JSON da API.
      var responseBody = await response.Content.ReadAsStringAsync();
      using var jsonDoc = JsonDocument.Parse(responseBody);
      var root = jsonDoc.RootElement;


      // Verifica se houve erro na resposta da API.
      if (root.TryGetProperty("errors", out var errors))
      {
        Console.WriteLine("erro na API:");
        Console.WriteLine(errors.ToString());
        return;
      }


      // Verifica se o campo 'data' está presente na resposta.
      if (!root.TryGetProperty("data", out var dataElement))
      {
        Console.WriteLine(responseBody);
        return;
      }


      // Extrai informações de paginação e dos repositórios retornados.
      var searchElement = dataElement.GetProperty("search");
      hasNextPage = searchElement.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean();
      cursor = searchElement.GetProperty("pageInfo").GetProperty("endCursor").GetString();


      var nodes = searchElement.GetProperty("nodes").EnumerateArray();
      Console.WriteLine($"Página carregada: {searchElement.GetProperty("nodes").GetArrayLength()} repositórios");


      // Adiciona cada repositório encontrado à lista.
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

      // Atualiza o total de repositórios coletados.
      totalFetched = allRepos.Count;
      Console.WriteLine($"Total acumulado: {totalFetched}");
    }


    // Exibe o total de repositórios obtidos.
    Console.WriteLine($"repositorios obtidos: {allRepos.Count}");


    // Monta as linhas do arquivo CSV, começando pelo cabeçalho.
    var csvLines = new List<string>
    {
      "Nome,Descrição,URL,Stars,Forks,Criado Em,Última Atualização,Linguagem,Issues Totais,Issues Fechadas,PRs,Releases"
    };


    // Adiciona os dados de cada repositório ao CSV, escapando aspas e formatando datas.
    foreach (var repo in allRepos)
    {
      string descEscaped = repo.Description?.Replace("\"", "\"\"") ?? "";
      string nameEscaped = repo.Name?.Replace("\"", "\"\"") ?? "";
      string updatedAtStr = repo.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
      string createdAtStr = repo.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

      csvLines.Add($"\"{nameEscaped}\",\"{descEscaped}\",{repo.Url},{repo.Stars},{repo.Forks},{createdAtStr},{updatedAtStr},\"{repo.PrimaryLanguage}\",{repo.IssuesTotal},{repo.IssuesClosed},{repo.PullRequests},{repo.Releases}");
    }


    // Salva o arquivo CSV no diretório atual do projeto.
    string filePath = Path.Combine(Directory.GetCurrentDirectory(), "repositorios_populares.csv");
    await File.WriteAllLinesAsync(filePath, csvLines, Encoding.UTF8);

    // Indica que o processo foi finalizado.
    Console.WriteLine($"finalizado");
  }

  class Repository
  {
    /// <summary>Nome do repositório.</summary>
    public string? Name { get; set; }
    /// <summary>Descrição do repositório.</summary>
    public string? Description { get; set; }
    /// <summary>URL do repositório.</summary>
    public string? Url { get; set; }
    /// <summary>Número de estrelas.</summary>
    public int Stars { get; set; }
    /// <summary>Número de forks.</summary>
    public int Forks { get; set; }
    /// <summary>Data de criação.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Data da última atualização.</summary>
    public DateTime UpdatedAt { get; set; }
    /// <summary>Número de pull requests.</summary>
    public int PullRequests { get; set; }
    /// <summary>Número de releases.</summary>
    public int Releases { get; set; }
    /// <summary>Total de issues.</summary>
    public int IssuesTotal { get; set; }
    /// <summary>Total de issues fechadas.</summary>
    public int IssuesClosed { get; set; }
    /// <summary>Linguagem principal do repositório.</summary>
    public string? PrimaryLanguage { get; set; }
  }
}
