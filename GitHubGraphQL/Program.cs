using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

// gráficos
using ScottPlot;

class Program
{
    static async Task Main(string[] args)
    {
        string githubToken = "inserir_token_aqui";
        string endpoint = "https://api.github.com/graphql";
        var allRepos = new List<Repository>();
        string? cursor = null;
        bool hasNextPage = true;
        int totalFetched = 0;

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubApiApp", "1.0"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);

        while (hasNextPage && totalFetched <1000)
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
                    PrimaryLanguage = node.TryGetProperty("primaryLanguage", out var lang) &&
                                      lang.ValueKind == JsonValueKind.Object &&
                                      lang.TryGetProperty("name", out var langName)
                                        ? langName.GetString() ?? ""
                                        : "",
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

        // --- CSV ---
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
        Console.WriteLine($"CSV gerado em {filePath}");

        // --- GRÁFICOS COM SCOTTPLOT ---
        Console.WriteLine("Gerando gráficos...");

        // 1. Idade do repositório (calculado a partir da data de criação)
        var plt1 = new ScottPlot.Plot(800, 500);
        var repoAges = allRepos.Select(r => (DateTime.Now - r.CreatedAt).TotalDays).ToArray();
        var repoNames = allRepos.Select(r => r.Name ?? "Unknown").ToArray();
        
        plt1.AddBar(repoAges, color: System.Drawing.Color.SkyBlue);
        plt1.Title("Idade dos Repositórios (em dias)");
        plt1.XLabel("Repositórios");
        plt1.YLabel("Idade (dias)");
        
        // Adicionar labels dos repositórios no eixo X
        var positions = Enumerable.Range(0, repoNames.Length).Select(i => (double)i).ToArray();
        plt1.XAxis.ManualTickPositions(positions, repoNames);
        
        plt1.SaveFig("idade_repositorios.png");
        Console.WriteLine("Gráfico 1 salvo: idade_repositorios.png");

        // 2. Total de pull requests aceitas
        var plt2 = new ScottPlot.Plot(800, 500);
        var pullRequests = allRepos.Select(r => (double)r.PullRequests).ToArray();
        
        plt2.AddBar(pullRequests, color: System.Drawing.Color.Green);
        plt2.Title("Total de Pull Requests por Repositório");
        plt2.XLabel("Repositórios");
        plt2.YLabel("Número de Pull Requests");
        
        plt2.XAxis.ManualTickPositions(positions, repoNames);
        
        plt2.SaveFig("total_pull_requests.png");
        Console.WriteLine("Gráfico 2 salvo: total_pull_requests.png");

        // 3. Total de releases
        var plt3 = new ScottPlot.Plot(800, 500);
        var releases = allRepos.Select(r => (double)r.Releases).ToArray();
        
        plt3.AddBar(releases, color: System.Drawing.Color.Orange);
        plt3.Title("Total de Releases por Repositório");
        plt3.XLabel("Repositórios");
        plt3.YLabel("Número de Releases");
        
        plt3.XAxis.ManualTickPositions(positions, repoNames);
        
        plt3.SaveFig("total_releases.png");
        Console.WriteLine("Gráfico 3 salvo: total_releases.png");

        // 4. Tempo até a última atualização (calculado a partir da data de última atualização)
        var plt4 = new ScottPlot.Plot(800, 500);
        var timeSinceUpdate = allRepos.Select(r => (DateTime.Now - r.UpdatedAt).TotalDays).ToArray();
        
        plt4.AddBar(timeSinceUpdate, color: System.Drawing.Color.Red);
        plt4.Title("Tempo desde a Última Atualização (em dias)");
        plt4.XLabel("Repositórios");
        plt4.YLabel("Dias desde última atualização");
        
        plt4.XAxis.ManualTickPositions(positions, repoNames);
        
        plt4.SaveFig("tempo_ultima_atualizacao.png");
        Console.WriteLine("Gráfico 4 salvo: tempo_ultima_atualizacao.png");

        // 5. Linguagem primária de cada repositório
        var plt5 = new ScottPlot.Plot(800, 500);
        var languages = allRepos.Select(r => r.PrimaryLanguage ?? "Unknown").ToArray();
        var languageCounts = languages.GroupBy(l => l)
                                     .Select(g => new { Language = g.Key, Count = g.Count() })
                                     .OrderByDescending(x => x.Count)
                                     .ToList();
        
        var langNames = languageCounts.Select(l => l.Language).ToArray();
        var langCounts = languageCounts.Select(l => (double)l.Count).ToArray();
        
        plt5.AddBar(langCounts, color: System.Drawing.Color.Purple);
        plt5.Title("Distribuição de Linguagens Primárias");
        plt5.XLabel("Linguagens");
        plt5.YLabel("Número de Repositórios");
        
        var langPositions = Enumerable.Range(0, langNames.Length).Select(i => (double)i).ToArray();
        plt5.XAxis.ManualTickPositions(langPositions, langNames);
        
        plt5.SaveFig("linguagens_primarias.png");
        Console.WriteLine("Gráfico 5 salvo: linguagens_primarias.png");

        // 6. Razão entre número de issues fechadas pelo total de issues
        var plt6 = new ScottPlot.Plot(800, 500);
        var issueRatios = allRepos.Select(r => 
            r.IssuesTotal > 0 ? (double)r.IssuesClosed / r.IssuesTotal : 0.0).ToArray();
        
        plt6.AddBar(issueRatios, color: System.Drawing.Color.Teal);
        plt6.Title("Razão de Issues Fechadas por Repositório");
        plt6.XLabel("Repositórios");
        plt6.YLabel("Razão (Issues Fechadas / Total Issues)");
        
        plt6.XAxis.ManualTickPositions(positions, repoNames);
        
        // Adicionar linha de referência em 0.5 (50%)
        plt6.AddHorizontalLine(0.5, color: System.Drawing.Color.Red, style: LineStyle.Dash);
        
        plt6.SaveFig("razao_issues_fechadas.png");
        Console.WriteLine("Gráfico 6 salvo: razao_issues_fechadas.png");

        Console.WriteLine("Todos os gráficos foram gerados com sucesso!");
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
