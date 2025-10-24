using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using ScottPlot;

namespace CaracterizandoAtividadeCodeReview
{
  class Program
  {
  private static readonly string GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
    private static readonly HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
      client.DefaultRequestHeaders.Add("User-Agent", "GitHubGraphQLClient");
      int repoLimit = 1000;
      try
      {
        var repoLimitEnv = Environment.GetEnvironmentVariable("REPO_LIMIT");
        if (!string.IsNullOrWhiteSpace(repoLimitEnv) && int.TryParse(repoLimitEnv, out int v)) repoLimit = v;
      }
      catch { }
      Console.WriteLine($"[debug] Repo limit = {repoLimit}");

      try
      {
        var envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
        {
          string maskedEnv = envToken.Length <= 8 ? envToken : envToken.Substring(0, 4) + "..." + envToken.Substring(envToken.Length - 4);
          Console.WriteLine($"[debug] env GITHUB_TOKEN detected (masked)='{maskedEnv}', length={envToken.Length}");
        }
        else
        {
          Console.WriteLine("[debug] env GITHUB_TOKEN environment variable is null or empty.");
        }

        if (!string.IsNullOrWhiteSpace(GitHubToken))
        {
          string maskedField = GitHubToken.Length <= 8 ? GitHubToken : GitHubToken.Substring(0, 4) + "..." + GitHubToken.Substring(GitHubToken.Length - 4);
          Console.WriteLine($"[debug] static field GitHubToken is set (masked)='{maskedField}', length={GitHubToken.Length}");
        }
        else
        {
          Console.WriteLine("[debug] static field GitHubToken is null or whitespace.");
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"[debug] Could not read GITHUB_TOKEN environment variable or GitHubToken field: {ex.Message}");
      }
      if (!string.IsNullOrWhiteSpace(GitHubToken))
      {
  client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
  Console.WriteLine("[debug] Authorization header set. Starting repository collection...");

  var repositorios = await GetPopularRepositories(repoLimit);
        Console.WriteLine($"Selecionados {repositorios.Count} repositórios.");

        string outputFile = "dataset_pull_requests.csv";
        bool headerWritten = false;

        foreach (var repo in repositorios)
        {
          Console.WriteLine($"\n🔍 Coletando PRs de {repo.Owner}/{repo.Name}...");
          var prs = await GetPullRequests(repo.Owner, repo.Name, maxPages: 3);

          if (prs.Count > 0)
          {
            SaveToCsv(outputFile, prs, repo, headerWritten);
            headerWritten = true;
            Console.WriteLine($"✔ {prs.Count} PRs adicionados ao dataset.");
          }

          await Task.Delay(2000);
        }

        Console.WriteLine("\n✅ Coleta concluída. Dataset completo gerado!");
      }
      else
      {
        Console.WriteLine("GitHub token vazio — pulando coleta. Gerando gráficos a partir de CSV existente (se houver)...");
      }

      string datasetFile = File.Exists("dataset_pull_requests.csv") ? "dataset_pull_requests.csv" : "pull_requests.csv";
      if (File.Exists(datasetFile))
      {
        Console.WriteLine($"Gerando gráficos a partir de {datasetFile}...");
        try
        {
          GeneratePlotsFromCsv(datasetFile);
          Console.WriteLine("✔ Gráficos gerados: pr_per_month.png e review_time_histogram.png");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Erro ao gerar gráficos: {ex.Message}");
        }
      }
      else
      {
        Console.WriteLine($"Arquivo CSV não encontrado: {datasetFile}. Nenhum gráfico gerado.");
      }
    }
    public static async Task<List<RepositoryData>> GetPopularRepositories(int reposMax = 200)
    {
      var reposList = new List<RepositoryData>();
      string? cursor = null;
      bool hasNextPage = true;

      Console.WriteLine("[debug] GetPopularRepositories started");

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
        Console.WriteLine($"[debug] Sending GraphQL search request, cursor='{cursor ?? "(null)"}'...");
        var variables = new { cursor = cursor };
        var requestBody = new { query = query, variables = variables };
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.github.com/graphql", requestContent);
        Console.WriteLine($"[debug] HTTP response: {response.StatusCode}");
        string json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[debug] Response length: {json.Length}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors))
        {
          Console.WriteLine("Erro da API: " + errors.ToString());
          break;
        }

        var nodes = root.GetProperty("data").GetProperty("search").GetProperty("nodes");

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

        var pageInfo = root.GetProperty("data").GetProperty("search").GetProperty("pageInfo");
        hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
        cursor = pageInfo.GetProperty("endCursor").GetString();
      }

      return reposList;
    }

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
                        state
                        reviews { totalCount }
                        comments { totalCount }
                        participants { totalCount }
                      }
                    }
                  }
                }";

      for (int i = 0; i < maxPages; i++)
      {
        try
        {
          var variables = new { owner = owner, name = name, cursor = cursor };
          var requestBody = new { query = query, variables = variables };
          var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

          int maxRetries = 3;
          int attempt = 0;
          HttpResponseMessage response = null!;
          while (attempt < maxRetries)
          {
            response = await client.PostAsync("https://api.github.com/graphql", requestContent);
            if (response.IsSuccessStatusCode)
              break;

            Console.WriteLine($"[warn] HTTP {response.StatusCode} while fetching PRs for {owner}/{name} (attempt {attempt + 1}/{maxRetries}). Retrying...");
            attempt++;
            await Task.Delay(2000 * attempt);
          }

          if (response == null)
          {
            Console.WriteLine($"[error] No HTTP response received for {owner}/{name}. Skipping.");
            break;
          }

          string json = await response.Content.ReadAsStringAsync();

          if (string.IsNullOrWhiteSpace(json))
          {
            Console.WriteLine($"[warn] Empty response body for {owner}/{name}. Skipping this page.");
            break;
          }
          if (json.TrimStart().StartsWith("<"))
          {
            string snippet = json.Length > 200 ? json.Substring(0, 200) : json;
            Console.WriteLine($"[error] Non-JSON response while fetching PRs for {owner}/{name}. Response starts with '<' (likely HTML). Snippet: {snippet}");
            break;
          }

          JsonDocument doc;
          try
          {
            doc = JsonDocument.Parse(json);
          }
          catch (Exception jex)
          {
            Console.WriteLine($"[error] Failed to parse JSON for {owner}/{name}: {jex.Message}. Response snippet: { (json.Length>200? json.Substring(0,200): json) }");
            break;
          }

          using (doc)
          {
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors))
            {
              Console.WriteLine("Erro: " + errors.ToString());
              break;
            }

            var repoNode = root.GetProperty("data").GetProperty("repository");
            var pullRequests = repoNode.GetProperty("pullRequests");

            foreach (var pr in pullRequests.GetProperty("nodes").EnumerateArray())
            {
              if (pr.GetProperty("closedAt").ValueKind == JsonValueKind.Null) continue;

              int reviews = pr.GetProperty("reviews").GetProperty("totalCount").GetInt32();
              DateTime created = pr.GetProperty("createdAt").GetDateTime();
              DateTime closed = pr.GetProperty("closedAt").GetDateTime();
              double reviewTime = (closed - created).TotalHours;

              if (reviewTime >= 1 && reviews >= 1)
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
                  Participants = pr.GetProperty("participants").GetProperty("totalCount").GetInt32(),
                  Status = pr.GetProperty("state").GetString() ?? ""
                });
              }
            }

            var pageInfo = pullRequests.GetProperty("pageInfo");
            bool hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
            if (!hasNextPage) break;
            cursor = pageInfo.GetProperty("endCursor").GetString();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[error] Exception while fetching PRs for {owner}/{name}: {ex.Message}");
          break;
        }
      }

      return prList;
    }


    public static void SaveToCsv(string filePath, List<PullRequestData> prs, RepositoryData repo, bool append)
    {
      using var writer = new StreamWriter(filePath, append);
      if (!append)
      {
        writer.WriteLine("Repository,Number,Title,BodyLength,CreatedAt,ClosedAt,ReviewTimeHours,Additions,Deletions,ChangedFiles,Reviews,Comments,Participants,Status");
      }

      foreach (var pr in prs)
      {
        writer.WriteLine($"{repo.Owner}/{repo.Name},{pr.Number},\"{pr.Title.Replace("\"", "'")}\",{pr.BodyLength},{pr.CreatedAt},{pr.ClosedAt},{pr.ReviewTimeHours},{pr.Additions},{pr.Deletions},{pr.ChangedFiles},{pr.Reviews},{pr.Comments},{pr.Participants},{pr.Status}");
      }
    }

    public static void GeneratePlotsFromCsv(string csvPath)
    {
      var lines = File.ReadAllLines(csvPath);
      if (lines.Length <= 1) return;

      var header = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToList();
      int idxRepository = header.FindIndex(h => string.Equals(h, "Repository", StringComparison.OrdinalIgnoreCase));
      int idxCreatedAt = header.FindIndex(h => string.Equals(h, "CreatedAt", StringComparison.OrdinalIgnoreCase));
      int idxReviewTime = header.FindIndex(h => string.Equals(h, "ReviewTimeHours", StringComparison.OrdinalIgnoreCase));
      int idxAdditions = header.FindIndex(h => string.Equals(h, "Additions", StringComparison.OrdinalIgnoreCase));
      int idxChangedFiles = header.FindIndex(h => string.Equals(h, "ChangedFiles", StringComparison.OrdinalIgnoreCase));
      int idxStatus = header.FindIndex(h => string.Equals(h, "Status", StringComparison.OrdinalIgnoreCase));
      int idxBodyLength = header.FindIndex(h => string.Equals(h, "BodyLength", StringComparison.OrdinalIgnoreCase));
      int idxReviews = header.FindIndex(h => string.Equals(h, "Reviews", StringComparison.OrdinalIgnoreCase));
      int idxComments = header.FindIndex(h => string.Equals(h, "Comments", StringComparison.OrdinalIgnoreCase));
      int idxParticipants = header.FindIndex(h => string.Equals(h, "Participants", StringComparison.OrdinalIgnoreCase));
      int idxDeletions = header.FindIndex(h => string.Equals(h, "Deletions", StringComparison.OrdinalIgnoreCase));

      var createdDates = new List<DateTime>();
      var reviewTimes = new List<double>();
      var additions = new List<double>();
      var changedFiles = new List<int>();
      var repoNames = new List<string?>();
      var statuses = new List<string?>();
      var bodyLengths = new List<double>();
      var reviews = new List<double>();
      var comments = new List<double>();
      var participants = new List<double>();
      var deletions = new List<double>();

      var reviewTimesByRepo = new Dictionary<string, List<double>>();
      var prCountByRepo = new Dictionary<string, int>();

      for (int i = 1; i < lines.Length; i++)
      {
        var fields = SplitCsvLine(lines[i]);
        if (fields.Count == 0) continue;

        string? repo = idxRepository >= 0 && idxRepository < fields.Count ? fields[idxRepository] : null;
        // Robust status detection: prefer the column index, but fall back to searching for MERGED/CLOSED
        string? status = null;
        if (idxStatus >= 0 && idxStatus < fields.Count) status = fields[idxStatus];
        if (string.IsNullOrWhiteSpace(status) || !(status.Trim().Equals("MERGED", StringComparison.OrdinalIgnoreCase) || status.Trim().Equals("CLOSED", StringComparison.OrdinalIgnoreCase)))
        {
          var candidate = fields.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && (f.Trim().Equals("MERGED", StringComparison.OrdinalIgnoreCase) || f.Trim().Equals("CLOSED", StringComparison.OrdinalIgnoreCase)));
          if (!string.IsNullOrWhiteSpace(candidate)) status = candidate;
        }
        string createdStr = idxCreatedAt >= 0 && idxCreatedAt < fields.Count ? fields[idxCreatedAt] : (fields.Count > 3 ? fields[3] : "");
        string reviewTimeStr = idxReviewTime >= 0 && idxReviewTime < fields.Count ? fields[idxReviewTime] : (fields.Count > 5 ? fields[5] : "");
        string additionsStr = idxAdditions >= 0 && idxAdditions < fields.Count ? fields[idxAdditions] : (fields.Count > 6 ? fields[6] : "");
        string changedStr = idxChangedFiles >= 0 && idxChangedFiles < fields.Count ? fields[idxChangedFiles] : (fields.Count > 8 ? fields[8] : "");
        string bodyLenStr = idxBodyLength >= 0 && idxBodyLength < fields.Count ? fields[idxBodyLength] : (fields.Count > 2 ? fields[2] : "");
        string reviewsStr = idxReviews >= 0 && idxReviews < fields.Count ? fields[idxReviews] : (fields.Count > 9 ? fields[9] : "");
        string commentsStr = idxComments >= 0 && idxComments < fields.Count ? fields[idxComments] : (fields.Count > 10 ? fields[10] : "");
        string participantsStr = idxParticipants >= 0 && idxParticipants < fields.Count ? fields[idxParticipants] : (fields.Count > 11 ? fields[11] : "");
        string deletionsStr = idxDeletions >= 0 && idxDeletions < fields.Count ? fields[idxDeletions] : (fields.Count > 7 ? fields[7] : "");

        if (!DateTimeTryParse(createdStr, out DateTime created)) continue;

        double.TryParse(reviewTimeStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double reviewTime);
        double.TryParse(additionsStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double addVal);
        int.TryParse(changedStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out int changedVal);
        double.TryParse(bodyLenStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double bodyLenVal);
        double.TryParse(reviewsStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double reviewsVal);
        double.TryParse(commentsStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double commentsVal);
        double.TryParse(participantsStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double participantsVal);
        double.TryParse(deletionsStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double deletionsVal);

        createdDates.Add(created);
        reviewTimes.Add(reviewTime);
        additions.Add(double.IsNaN(addVal) ? 0 : addVal);
        changedFiles.Add(changedVal);
        repoNames.Add(string.IsNullOrWhiteSpace(repo) ? null : repo);
        statuses.Add(string.IsNullOrWhiteSpace(status) ? null : status);
        bodyLengths.Add(bodyLenVal);
        reviews.Add(reviewsVal);
        comments.Add(commentsVal);
        participants.Add(participantsVal);
        deletions.Add(deletionsVal);

        if (!string.IsNullOrWhiteSpace(repo))
        {
          if (!reviewTimesByRepo.ContainsKey(repo!)) reviewTimesByRepo[repo!] = new List<double>();
          reviewTimesByRepo[repo!].Add(reviewTime);

          if (!prCountByRepo.ContainsKey(repo!)) prCountByRepo[repo!] = 0;
          prCountByRepo[repo!]++;
        }
      }

      if (createdDates.Count == 0) return;

      // Agrupar por mês (ano-mês)
      var countsByMonth = new SortedDictionary<string, int>();
      for (int i = 0; i < createdDates.Count; i++)
      {
        var d = createdDates[i];
        string key = d.ToString("yyyy-MM");
        if (!countsByMonth.ContainsKey(key)) countsByMonth[key] = 0;
        countsByMonth[key]++;
      }


      int n = countsByMonth.Count;
      double[] xs = new double[n];
      double[] ys = new double[n];
      int idx = 0;
      foreach (var kv in countsByMonth)
      {
        DateTime dt = DateTime.ParseExact(kv.Key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        xs[idx] = dt.ToOADate();
        ys[idx] = kv.Value;
        idx++;
      }


      var plt = new ScottPlot.Plot(1200, 600);
      plt.AddScatter(xs, ys, label: "PRs por mês", lineWidth: 2);
      plt.XAxis.DateTimeFormat(true);
      plt.Title("Pull Requests por Mês");
      plt.YLabel("Quantidade de PRs");
  SavePlot(plt, "pr_per_month.png");

      double[] rt = reviewTimes.ToArray();
      if (rt.Length > 0)
      {
        int bins = 50;
        double min = rt.Min();
        double max = rt.Max();
        if (min == max) max = min + 1;
        double width = (max - min) / bins;
        double[] counts = new double[bins];
        double[] centers = new double[bins];
        foreach (var v in rt)
        {
          int b = (int)Math.Floor((v - min) / width);
          if (b < 0) b = 0;
          if (b >= bins) b = bins - 1;
          counts[b]++;
        }
        for (int i = 0; i < bins; i++) centers[i] = min + (i + 0.5) * width;

        var plt2 = new ScottPlot.Plot(1200, 600);
        plt2.AddBar(counts, centers);
        plt2.Title("Histograma: Tempo de revisão (horas)");
        plt2.XLabel("Horas");
        plt2.YLabel("Frequência");
  SavePlot(plt2, "review_time_histogram.png");
      }

     
      try
      {
        var plt3 = new ScottPlot.Plot(1000, 700);
        double[] addArr = additions.ToArray();
        double[] rtArr = reviewTimes.ToArray();
        plt3.AddScatter(addArr, rtArr, markerSize: 5, label: "Additions x ReviewTime");
        plt3.XLabel("Adições");
        plt3.YLabel("Tempo de revisão (horas)");
        double rhoAddRev = ComputeSpearman(addArr.ToList(), rtArr.ToList());
        plt3.Title($"Adições vs Tempo de Revisão (Spearman \u03C1={rhoAddRev:F2})");
  SavePlot(plt3, "additions_vs_reviewtime.png");
      }
      catch { }

    
      try
      {
        double[] chArr = Array.ConvertAll(changedFiles.ToArray(), v => (double)v);
        if (chArr.Length > 0)
        {
          int bins2 = 30;
          double min2 = chArr.Min();
          double max2 = chArr.Max();
          if (min2 == max2) max2 = min2 + 1;
          double width2 = (max2 - min2) / bins2;
          double[] counts2 = new double[bins2];
          double[] centers2 = new double[bins2];
          foreach (var v in chArr)
          {
            int b = (int)Math.Floor((v - min2) / width2);
            if (b < 0) b = 0;
            if (b >= bins2) b = bins2 - 1;
            counts2[b]++;
          }
          for (int i = 0; i < bins2; i++) centers2[i] = min2 + (i + 0.5) * width2;

          var plt4 = new ScottPlot.Plot(1000, 600);
          plt4.AddBar(counts2, centers2);
          plt4.Title("Histograma: Arquivos modificados por PR");
          plt4.XLabel("Changed files");
          plt4.YLabel("Frequência");
          SavePlot(plt4, "changed_files_histogram.png");
        }
      }
      catch { }


      if (prCountByRepo.Count > 0)
      {
        var top = new List<KeyValuePair<string, int>>(prCountByRepo);
        top.Sort((a, b) => b.Value.CompareTo(a.Value));
        int take = Math.Min(10, top.Count);
        var plt5 = new ScottPlot.Plot(1200, 600);
        double[] vals = new double[take];
        string[] labels = new string[take];
        for (int i = 0; i < take; i++) { vals[i] = top[i].Value; labels[i] = top[i].Key; }
        plt5.AddBar(vals);
        plt5.XTicks(Enumerable.Range(0, take).Select(x => (double)x).ToArray(), labels);
        plt5.Title("Top repositories (PRs no dataset)");
        plt5.YLabel("Quantidade PRs");
  SavePlot(plt5, "pr_count_by_repo.png");

        try
        {
          int takeAvg = Math.Min(10, top.Count);
          double[] avgVals = new double[takeAvg];
          string[] avgLabels = new string[takeAvg];
          for (int i = 0; i < takeAvg; i++)
          {
            var repoName = top[i].Key;
            avgLabels[i] = repoName;
            if (reviewTimesByRepo.TryGetValue(repoName, out var list) && list.Count > 0)
              avgVals[i] = list.Average();
            else
              avgVals[i] = 0;
          }

          var plt6 = new ScottPlot.Plot(1200, 700);
          plt6.AddBar(avgVals);
          plt6.XTicks(Enumerable.Range(0, takeAvg).Select(x => (double)x).ToArray(), avgLabels);
          plt6.Title("Média do tempo de revisão por repositório (top)");
          plt6.YLabel("Horas (média)");
          SavePlot(plt6, "avg_review_time_by_repo.png");
        }
        catch { }
      }

  
      bool hasStatus = statuses.Any(s => !string.IsNullOrWhiteSpace(s));
      if (hasStatus)
      {
    
        var statusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in statuses.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
          string key = s!;
          if (!statusCounts.ContainsKey(key)) statusCounts[key] = 0;
          statusCounts[key]++;
        }
  var pltStatus = new ScottPlot.Plot(800, 600);
  double[] valsStatus = statusCounts.Values.Select(v => (double)v).ToArray();
  string[] labelsStatus = statusCounts.Keys.ToArray();
  pltStatus.AddBar(valsStatus);
  pltStatus.XTicks(Enumerable.Range(0, labelsStatus.Length).Select(x => (double)x).ToArray(), labelsStatus);
  pltStatus.Title("Proporção de Status (MERGED vs CLOSED)");
  pltStatus.YLabel("Quantidade");
  SavePlot(pltStatus, "status_proportion.png");
  Console.WriteLine("[status-keys] " + string.Join(",", statusCounts.Keys));

        string[] metrics = new[] { "Additions", "ChangedFiles", "ReviewTimeHours", "BodyLength", "Comments", "Participants" };
        for (int m = 0; m < metrics.Length; m++)
        {
          string metric = metrics[m];
          var mergedVals = new List<double>();
          var closedVals = new List<double>();
          for (int i = 0; i < statuses.Count; i++)
          {
            if (string.IsNullOrWhiteSpace(statuses[i])) continue;
            string st = statuses[i]!.Trim().ToUpperInvariant();
            double val = 0;
            switch (metric)
            {
              case "Additions": val = additions[i]; break;
              case "ChangedFiles": val = changedFiles[i]; break;
              case "ReviewTimeHours": val = reviewTimes[i]; break;
              case "BodyLength": val = bodyLengths[i]; break;
              case "Comments": val = comments[i]; break;
              case "Participants": val = participants[i]; break;
            }
            if (st.StartsWith("MERG")) mergedVals.Add(val);
            else closedVals.Add(val);
          }

          Console.WriteLine($"[boxplot-pre] {metric}: merged={mergedVals.Count}, closed={closedVals.Count}");
          if (mergedVals.Count == 0 || closedVals.Count == 0) continue;

          double[] medians = new double[2];
          double[] lowerErr = new double[2];
          double[] upperErr = new double[2];

          void FillStats(List<double> list, int idx)
          {
            var arr = list.OrderBy(x => x).ToArray();
            double median = arr[arr.Length / 2];
            double q1 = arr[(int)(arr.Length * 0.25)];
            double q3 = arr[(int)(arr.Length * 0.75)];
            medians[idx] = median;
            lowerErr[idx] = median - q1;
            upperErr[idx] = q3 - median;
          }

          FillStats(mergedVals, 0);
          FillStats(closedVals, 1);
          Console.WriteLine($"[boxplot] {metric}: merged={mergedVals.Count}, closed={closedVals.Count}");

          var pltBox = new ScottPlot.Plot(800, 600);
          double[] xsBox = new double[] { 0, 1 };
      
          pltBox.AddErrorBars(xsBox, medians, upperErr, lowerErr);
          pltBox.AddScatter(xsBox, medians, markerSize: 8, markerShape: ScottPlot.MarkerShape.filledCircle);
          pltBox.XTicks(new double[] { 0, 1 }, new string[] { "MERGED", "CLOSED" });
          pltBox.Title($"{metric}: MERGED vs CLOSED (mediana ± IQR)");
          pltBox.YLabel(metric);
          SavePlot(pltBox, $"boxplot_like_{metric}.png");
        }
      }


      try
      {
        var pltRQ5 = new ScottPlot.Plot(1000, 700);
        double[] x05 = additions.ToArray();
        double[] y05 = reviews.ToArray();
        pltRQ5.AddScatter(x05, y05, markerSize: 5);
        double rho05 = ComputeSpearman(x05.ToList(), y05.ToList());
        pltRQ5.Title($"Additions vs Reviews (Spearman \u03C1={rho05:F2})");
        pltRQ5.XLabel("Additions");
        pltRQ5.YLabel("Reviews");
  SavePlot(pltRQ5, "rq05_additions_reviews.png");
      }
      catch { }

      try
      {
        var pltRQ6 = new ScottPlot.Plot(1000, 700);
        double[] x06 = reviewTimes.ToArray();
        double[] y06 = reviews.ToArray();
        pltRQ6.AddScatter(x06, y06, markerSize: 5);
        double rho06 = ComputeSpearman(x06.ToList(), y06.ToList());
        pltRQ6.Title($"ReviewTimeHours vs Reviews (Spearman \u03C1={rho06:F2})");
        pltRQ6.XLabel("ReviewTimeHours");
        pltRQ6.YLabel("Reviews");
  SavePlot(pltRQ6, "rq06_reviewtime_reviews.png");
      }
      catch { }


      try
      {
        var pltRQ7 = new ScottPlot.Plot(1000, 700);
        double[] x07 = bodyLengths.ToArray();
        double[] y07 = reviews.ToArray();
        pltRQ7.AddScatter(x07, y07, markerSize: 5);
        double rho07 = ComputeSpearman(x07.ToList(), y07.ToList());
        pltRQ7.Title($"BodyLength vs Reviews (Spearman \u03C1={rho07:F2})");
        pltRQ7.XLabel("BodyLength");
        pltRQ7.YLabel("Reviews");
  SavePlot(pltRQ7, "rq07_bodylength_reviews.png");
      }
      catch { }


      try
      {
        var pltRQ8 = new ScottPlot.Plot(1000, 700);
        double[] x08 = comments.ToArray();
        double[] y08 = reviews.ToArray();
        pltRQ8.AddScatter(x08, y08, markerSize: 5);
        double rho08 = ComputeSpearman(x08.ToList(), y08.ToList());
        pltRQ8.Title($"Comments vs Reviews (Spearman \u03C1={rho08:F2})");
        pltRQ8.XLabel("Comments");
        pltRQ8.YLabel("Reviews");
  SavePlot(pltRQ8, "rq08_comments_reviews.png");
      }
      catch { }


      try
      {
        var numericSeries = new Dictionary<string, double[]>() {
          { "Additions", additions.ToArray() },
          { "Deletions", deletions.ToArray() },
          { "BodyLength", bodyLengths.ToArray() },
          { "ReviewTimeHours", reviewTimes.ToArray() },
          { "Reviews", reviews.ToArray() },
          { "Comments", comments.ToArray() },
          { "Participants", participants.ToArray() },
          { "ChangedFiles", changedFiles.Select(v => (double)v).ToArray() }
        };

        var keys = numericSeries.Keys.ToArray();
        int k = keys.Length;
        double[,] mat = new double[k, k];
        for (int i = 0; i < k; i++)
        {
          for (int j = 0; j < k; j++)
          {
            mat[i, j] = ComputeSpearman(numericSeries[keys[i]].ToList(), numericSeries[keys[j]].ToList());
          }
        }

        var pltHeat = new ScottPlot.Plot(800, 800);
        var hm = pltHeat.AddHeatmap(mat);
        hm.Update(mat);
        pltHeat.Title("Matriz de Correlação (Spearman)");
        pltHeat.XTicks(Enumerable.Range(0, k).Select(x => (double)x).ToArray(), keys);
        pltHeat.YTicks(Enumerable.Range(0, k).Select(x => (double)x).ToArray(), keys);
        SavePlot(pltHeat, "correlation_spearman_heatmap.png");
      }
      catch { }
    }


    private static void SavePlot(ScottPlot.Plot plt, string filename)
    {
      try
      {
        plt.SaveFig(filename);
        Console.WriteLine($"[save] {filename}");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[error] Failed to save {filename}: {ex.Message}");
      }
    }
    private static double ComputeSpearman(List<double> a, List<double> b)
    {
      if (a == null || b == null) return double.NaN;
      if (a.Count != b.Count || a.Count < 2) return double.NaN;

      double[] ra = Rankify(a.ToArray());
      double[] rb = Rankify(b.ToArray());

      double meanA = ra.Average();
      double meanB = rb.Average();
      double num = 0; double denA = 0; double denB = 0;
      for (int i = 0; i < ra.Length; i++)
      {
        double da = ra[i] - meanA;
        double db = rb[i] - meanB;
        num += da * db;
        denA += da * da;
        denB += db * db;
      }
      double denom = Math.Sqrt(denA * denB);
      if (denom == 0) return double.NaN;
      return num / denom;
    }


    private static double[] Rankify(double[] values)
    {
      int n = values.Length;
      var idx = Enumerable.Range(0, n).ToArray();
      Array.Sort(values, idx, Comparer<double>.Default);
      var ranks = new double[n];
      int i = 0;
      while (i < n)
      {
        int j = i + 1;
        while (j < n && values[j] == values[i]) j++;
        double avgRank = (i + j - 1) / 2.0 + 1; 
        for (int k2 = i; k2 < j; k2++) ranks[k2] = avgRank;
        i = j;
      }
   
      var ranksByIndex = new double[n];
      for (int k2 = 0; k2 < n; k2++) ranksByIndex[idx[k2]] = ranks[k2];
      return ranksByIndex;
    }

    private static bool DateTimeTryParse(string input, out DateTime result)
    {
      if (DateTime.TryParseExact(input, new[] { "dd/MM/yyyy HH:mm:ss", "d/M/yyyy H:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-dd HH:mm:ss" }, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out result))
        return true;

      if (DateTime.TryParse(input, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out result))
        return true;

      if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        return true;

      return false;
    }

  
    private static List<string> SplitCsvLine(string line)
    {
      var fields = new List<string>();
      if (string.IsNullOrEmpty(line)) return fields;

      var sb = new StringBuilder();
      bool inQuotes = false;
      for (int i = 0; i < line.Length; i++)
      {
        char c = line[i];
        if (c == '"')
        {
        
          if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
          {
            sb.Append('"');
            i++; 
          }
          else
          {
            inQuotes = !inQuotes;
          }
        }
        else if (c == ',' && !inQuotes)
        {
          fields.Add(sb.ToString());
          sb.Clear();
        }
        else
        {
          sb.Append(c);
        }
      }
      fields.Add(sb.ToString());
      return fields;
    }
  }

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
    public string Status { get; set; } = "";
  }

  public class RepositoryData
  {
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public int PullRequestCount { get; set; }
  }
}