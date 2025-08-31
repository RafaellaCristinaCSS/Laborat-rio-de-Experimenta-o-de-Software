
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

/// <summary>
/// Programa para buscar repositórios populares do GitHub usando a API GraphQL,
/// extrair informações relevantes, salvar em um arquivo CSV e gerar gráficos automáticos das principais métricas.
/// </summary>

/// <summary>
/// Classe principal do programa. Realiza a coleta dos dados dos repositórios e gera os gráficos.
/// </summary>
class Program
{
    /// <summary>
    /// Ponto de entrada do programa. Busca repositórios populares do GitHub, salva os dados em CSV
    /// e gera gráficos dos top 10 "mais" e "menos" de cada métrica relevante.
    /// </summary>
    /// <param name="args">Argumentos de linha de comando (não utilizados).</param>
    static async Task Main(string[] args)
    {
        // Token de autenticação do GitHub. Deve ser preenchido pelo usuário.
        string githubToken = "inserir_token_aqui";
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

        // --- CSV ---

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
        Console.WriteLine($"CSV gerado em {filePath}");

        // --- GRÁFICOS COM SCOTTPLOT ---
        // Geração dos gráficos automáticos usando ScottPlot.
        Console.WriteLine("Gerando gráficos...");

        // 1. Idade do repositório (em dias) - Top 10 mais antigos
        var reposByAge = allRepos.OrderByDescending(r => (DateTime.Now - r.CreatedAt).TotalDays).Take(10).ToList();
        var repoNamesAge = reposByAge.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsAge = Enumerable.Range(0, repoNamesAge.Length).Select(i => (double)i).ToArray();
        var plt1 = new ScottPlot.Plot(1200, 600);
        var repoAges = reposByAge.Select(r => (DateTime.Now - r.CreatedAt).TotalDays).ToArray();
        plt1.AddBar(repoAges, color: System.Drawing.Color.SkyBlue);
        plt1.Title("Idade dos Repositórios (em dias) - Top 10 Mais Antigos");
        plt1.XLabel("Repositórios");
        plt1.YLabel("Idade (dias)");
        plt1.XAxis.ManualTickPositions(positionsAge, repoNamesAge);
        plt1.SaveFig("idade_repositorios.png");
        Console.WriteLine("Gráfico 1 salvo: idade_repositorios.png");

        // 1b. Idade do repositório (em dias) - Top 10 mais novos
        var reposByAgeNew = allRepos.OrderBy(r => (DateTime.Now - r.CreatedAt).TotalDays).Take(10).ToList();
        var repoNamesAgeNew = reposByAgeNew.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsAgeNew = Enumerable.Range(0, repoNamesAgeNew.Length).Select(i => (double)i).ToArray();
        var plt1b = new ScottPlot.Plot(1200, 600);
        var repoAgesNew = reposByAgeNew.Select(r => (DateTime.Now - r.CreatedAt).TotalDays).ToArray();
        plt1b.AddBar(repoAgesNew, color: System.Drawing.Color.LightGreen);
        plt1b.Title("Idade dos Repositórios (em dias) - Top 10 Mais Novos");
        plt1b.XLabel("Repositórios");
        plt1b.YLabel("Idade (dias)");
        plt1b.XAxis.ManualTickPositions(positionsAgeNew, repoNamesAgeNew);
        plt1b.SaveFig("idade_repositorios_novos.png");
        Console.WriteLine("Gráfico 1b salvo: idade_repositorios_novos.png");

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

        // 2b. Total de pull requests aceitas - Top 10 com menos PRs
        var reposByPRsMenos = allRepos.OrderBy(r => r.PullRequests).Take(10).ToList();
        var repoNamesPRsMenos = reposByPRsMenos.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsPRsMenos = Enumerable.Range(0, repoNamesPRsMenos.Length).Select(i => (double)i).ToArray();
        var plt2b = new ScottPlot.Plot(1200, 600);
        var pullRequestsMenos = reposByPRsMenos.Select(r => (double)r.PullRequests).ToArray();
        plt2b.AddBar(pullRequestsMenos, color: System.Drawing.Color.LightPink);
        plt2b.Title("Total de Pull Requests por Repositório - Top 10 com Menos PRs");
        plt2b.XLabel("Repositórios");
        plt2b.YLabel("Número de Pull Requests");
        plt2b.XAxis.ManualTickPositions(positionsPRsMenos, repoNamesPRsMenos);
        plt2b.SaveFig("total_pull_requests_menos.png");
        Console.WriteLine("Gráfico 2b salvo: total_pull_requests_menos.png");

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

        // 3b. Total de releases - Top 10 com menos releases
        var reposByReleasesMenos = allRepos.OrderBy(r => r.Releases).Take(10).ToList();
        var repoNamesReleasesMenos = reposByReleasesMenos.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsReleasesMenos = Enumerable.Range(0, repoNamesReleasesMenos.Length).Select(i => (double)i).ToArray();
        var plt3b = new ScottPlot.Plot(1200, 600);
        var releasesMenos = reposByReleasesMenos.Select(r => (double)r.Releases).ToArray();
        plt3b.AddBar(releasesMenos, color: System.Drawing.Color.LightYellow);
        plt3b.Title("Total de Releases por Repositório - Top 10 com Menos Releases");
        plt3b.XLabel("Repositórios");
        plt3b.YLabel("Número de Releases");
        plt3b.XAxis.ManualTickPositions(positionsReleasesMenos, repoNamesReleasesMenos);
        plt3b.SaveFig("total_releases_menos.png");
        Console.WriteLine("Gráfico 3b salvo: total_releases_menos.png");

        // 4. Tempo até a última atualização (em dias) - Top 10 mais antigos
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

        // 4b. Tempo até a última atualização (em dias) - Top 10 mais recentes
        var reposByUpdateTimeNovo = allRepos.OrderBy(r => (DateTime.Now - r.UpdatedAt).TotalDays).Take(10).ToList();
        var repoNamesUpdateTimeNovo = reposByUpdateTimeNovo.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsUpdateTimeNovo = Enumerable.Range(0, repoNamesUpdateTimeNovo.Length).Select(i => (double)i).ToArray();
        var plt4b = new ScottPlot.Plot(1200, 600);
        var timeSinceUpdateNovo = reposByUpdateTimeNovo.Select(r => (DateTime.Now - r.UpdatedAt).TotalDays).ToArray();
        plt4b.AddBar(timeSinceUpdateNovo, color: System.Drawing.Color.LightCoral);
        plt4b.Title("Tempo desde a Última Atualização (em dias) - Top 10 Mais Recentes");
        plt4b.XLabel("Repositórios");
        plt4b.YLabel("Dias desde última atualização");
        plt4b.XAxis.ManualTickPositions(positionsUpdateTimeNovo, repoNamesUpdateTimeNovo);
        plt4b.SaveFig("tempo_ultima_atualizacao_novos.png");
        Console.WriteLine("Gráfico 4b salvo: tempo_ultima_atualizacao_novos.png");

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
        plt6.AddHorizontalLine(0.5, color: System.Drawing.Color.Red, style: LineStyle.Dash);
        plt6.SaveFig("razao_issues_fechadas.png");
        Console.WriteLine("Gráfico 6 salvo: razao_issues_fechadas.png");

        // 6b. Razão entre número de issues fechadas pelo total de issues - Top 10 com menor razão
        var reposByIssueRatioMenor = allRepos.Where(r => r.IssuesTotal > 0)
                                            .OrderBy(r => (double)r.IssuesClosed / r.IssuesTotal)
                                            .Take(10).ToList();
        var repoNamesIssueRatioMenor = reposByIssueRatioMenor.Select(r => r.Name ?? "Unknown").ToArray();
        var positionsIssueRatioMenor = Enumerable.Range(0, repoNamesIssueRatioMenor.Length).Select(i => (double)i).ToArray();
        var plt6b = new ScottPlot.Plot(1200, 600);
        var issueRatiosMenor = reposByIssueRatioMenor.Select(r => 
            r.IssuesTotal > 0 ? (double)r.IssuesClosed / r.IssuesTotal : 0.0).ToArray();
        plt6b.AddBar(issueRatiosMenor, color: System.Drawing.Color.LightSlateGray);
        plt6b.Title("Razão de Issues Fechadas por Repositório - Top 10 com Menor Razão");
        plt6b.XLabel("Repositórios");
        plt6b.YLabel("Razão (Issues Fechadas / Total Issues)");
        plt6b.XAxis.ManualTickPositions(positionsIssueRatioMenor, repoNamesIssueRatioMenor);
        plt6b.AddHorizontalLine(0.5, color: System.Drawing.Color.Red, style: LineStyle.Dash);
        plt6b.SaveFig("razao_issues_fechadas_menor.png");
        Console.WriteLine("Gráfico 6b salvo: razao_issues_fechadas_menor.png");

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

        // Indica que o processo foi finalizado.
        Console.WriteLine($"finalizado");
    }

    /// <summary>
    /// Classe para armazenar os dados de cada repositório coletado.
    /// </summary>
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