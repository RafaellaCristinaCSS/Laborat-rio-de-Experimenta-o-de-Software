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

        // 1. Idade do repositório (calculado a partir da data de criação) - Top 10 mais antigos
        var reposByAge = allRepos.OrderByDescending(r => (DateTime.Now - r.CreatedAt).TotalDays).Take(10).ToList();
        var repoNamesAge = reposByAge.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsAge = Enumerable.Range(0, repoNamesAge.Length).Select(i => (double)i).ToArray();
        
        var plt1 = new ScottPlot.Plot(1200, 600);
        var repoAges = reposByAge.Select(r => (DateTime.Now - r.CreatedAt).TotalDays).ToArray();
        
        plt1.AddBar(repoAges, color: System.Drawing.Color.SkyBlue);
        plt1.Title("Idade dos Repositórios (em dias) - Top 10 Mais Antigos");
        plt1.XLabel("Repositórios");
        plt1.YLabel("Idade (dias)");
        
        // Adicionar labels dos repositórios no eixo X
        plt1.XAxis.ManualTickPositions(positionsAge, repoNamesAge);
        
        plt1.SaveFig("idade_repositorios.png");
        Console.WriteLine("Gráfico 1 salvo: idade_repositorios.png");

        // 2. Total de pull requests aceitas - Top 10 com mais PRs
        var reposByPRs = allRepos.OrderByDescending(r => r.PullRequests).Take(10).ToList();
        var repoNamesPRs = reposByPRs.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsPRs = Enumerable.Range(0, repoNamesPRs.Length).Select(i => (double)i).ToArray();
        
        var plt2 = new ScottPlot.Plot(1200, 600);
        var pullRequests = reposByPRs.Select(r => (double)r.PullRequests).ToArray();
        
        plt2.AddBar(pullRequests, color: System.Drawing.Color.Green);
        plt2.Title("Total de Pull Requests por Repositório - Top 10 com Mais PRs");
        plt2.XLabel("Repositórios");
        plt2.YLabel("Número de Pull Requests");
        
        plt2.XAxis.ManualTickPositions(positionsPRs, repoNamesPRs);
        
        plt2.SaveFig("total_pull_requests.png");
        Console.WriteLine("Gráfico 2 salvo: total_pull_requests.png");

        // 3. Total de releases - Top 10 com mais releases
        var reposByReleases = allRepos.OrderByDescending(r => r.Releases).Take(10).ToList();
        var repoNamesReleases = reposByReleases.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsReleases = Enumerable.Range(0, repoNamesReleases.Length).Select(i => (double)i).ToArray();
        
        var plt3 = new ScottPlot.Plot(1200, 600);
        var releases = reposByReleases.Select(r => (double)r.Releases).ToArray();
        
        plt3.AddBar(releases, color: System.Drawing.Color.Orange);
        plt3.Title("Total de Releases por Repositório - Top 10 com Mais Releases");
        plt3.XLabel("Repositórios");
        plt3.YLabel("Número de Releases");
        
        plt3.XAxis.ManualTickPositions(positionsReleases, repoNamesReleases);
        
        plt3.SaveFig("total_releases.png");
        Console.WriteLine("Gráfico 3 salvo: total_releases.png");

        // 4. Tempo até a última atualização (calculado a partir da data de última atualização) - Top 10 mais antigos
        var reposByUpdateTime = allRepos.OrderByDescending(r => (DateTime.Now - r.UpdatedAt).TotalDays).Take(10).ToList();
        var repoNamesUpdateTime = reposByUpdateTime.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsUpdateTime = Enumerable.Range(0, repoNamesUpdateTime.Length).Select(i => (double)i).ToArray();
        
        var plt4 = new ScottPlot.Plot(1200, 600);
        var timeSinceUpdate = reposByUpdateTime.Select(r => (DateTime.Now - r.UpdatedAt).TotalDays).ToArray();
        
        plt4.AddBar(timeSinceUpdate, color: System.Drawing.Color.Red);
        plt4.Title("Tempo desde a Última Atualização (em dias) - Top 10 Mais Antigos");
        plt4.XLabel("Repositórios");
        plt4.YLabel("Dias desde última atualização");
        
        plt4.XAxis.ManualTickPositions(positionsUpdateTime, repoNamesUpdateTime);
        
        plt4.SaveFig("tempo_ultima_atualizacao.png");
        Console.WriteLine("Gráfico 4 salvo: tempo_ultima_atualizacao.png");

        // 5. Linguagem primária - Gráfico de barras para melhor visualização
        var plt5 = new ScottPlot.Plot(1200, 600);
        var languages = allRepos.Select(r => r.PrimaryLanguage ?? "Unknown").ToArray();
        var languageCounts = languages.GroupBy(l => l)
                                     .Select(g => new { Language = g.Key, Count = g.Count() })
                                     .OrderByDescending(x => x.Count)
                                     .Take(10) // Top 10 linguagens
                                     .ToList();
        
        var langNames = languageCounts.Select(l => l.Language).ToArray();
        var langCounts = languageCounts.Select(l => (double)l.Count).ToArray();
        var langPositions = Enumerable.Range(0, langNames.Length).Select(i => (double)i).ToArray();
        
        // Criar gráfico de barras
        plt5.AddBar(langCounts, color: System.Drawing.Color.Purple);
        plt5.Title("Distribuição de Linguagens Primárias - Top 10 Mais Utilizadas");
        plt5.XLabel("Linguagens");
        plt5.YLabel("Número de Repositórios");
        
        plt5.XAxis.ManualTickPositions(langPositions, langNames);
        
        plt5.SaveFig("linguagens_primarias.png");
        Console.WriteLine("Gráfico 5 salvo: linguagens_primarias.png");

        // 6. Razão entre número de issues fechadas pelo total de issues - Top 10 com maior razão
        var reposByIssueRatio = allRepos.Where(r => r.IssuesTotal > 0)
                                       .OrderByDescending(r => (double)r.IssuesClosed / r.IssuesTotal)
                                       .Take(10).ToList();
        var repoNamesIssueRatio = reposByIssueRatio.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsIssueRatio = Enumerable.Range(0, repoNamesIssueRatio.Length).Select(i => (double)i).ToArray();
        
        var plt6 = new ScottPlot.Plot(1200, 600);
        var issueRatios = reposByIssueRatio.Select(r => 
            r.IssuesTotal > 0 ? (double)r.IssuesClosed / r.IssuesTotal : 0.0).ToArray();
        
        plt6.AddBar(issueRatios, color: System.Drawing.Color.Teal);
        plt6.Title("Razão de Issues Fechadas por Repositório - Top 10 com Maior Razão");
        plt6.XLabel("Repositórios");
        plt6.YLabel("Razão (Issues Fechadas / Total Issues)");
        
        plt6.XAxis.ManualTickPositions(positionsIssueRatio, repoNamesIssueRatio);
        
        // Adicionar linha de referência em 0.5 (50%)
        plt6.AddHorizontalLine(0.5, color: System.Drawing.Color.Red, style: LineStyle.Dash);
        
        plt6.SaveFig("razao_issues_fechadas.png");
        Console.WriteLine("Gráfico 6 salvo: razao_issues_fechadas.png");

        // 7. Gráfico adicional: Distribuição de Stars (scatter plot)
        var plt7 = new ScottPlot.Plot(800, 500);
        var starCounts = allRepos.Select(r => (double)r.Stars).ToArray();
        var repoIndices = Enumerable.Range(0, starCounts.Length).Select(i => (double)i).ToArray();
        
        plt7.AddScatter(repoIndices, starCounts, color: System.Drawing.Color.Blue, markerSize: 3);
        plt7.Title("Distribuição de Stars - Todos os Repositórios");
        plt7.XLabel("Índice do Repositório");
        plt7.YLabel("Número de Stars");
        
        plt7.SaveFig("distribuicao_stars.png");
        Console.WriteLine("Gráfico 7 salvo: distribuicao_stars.png");

        Console.WriteLine("Todos os gráficos foram gerados com sucesso!");
        Console.WriteLine($"Nota: Cada gráfico mostra os top 10 repositórios ordenados por sua respectiva métrica.");
        Console.WriteLine($"O gráfico de linguagens mostra as top 10 linguagens mais utilizadas de todos os {allRepos.Count} repositórios.");
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
