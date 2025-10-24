# Caracterizando a Atividade de Code Review no GitHub (LAB03)

<p align="center">
  <img src="https://img.shields.io/badge/Linguagem-C%23-blueviolet" alt="Linguagem C#">
  <img src="https://img.shields.io/badge/Plataforma-.NET-blue" alt="Plataforma .NET">
  <img src="https://img.shields.io/badge/API-GraphQL-e10098" alt="API GraphQL">
</p>

## 📖 Descrição do Projeto

Este repositório contém o código e os artefatos desenvolvidos em C# como parte do **LAB03** da disciplina *Laboratório de Experimentação de Software*.

O objetivo principal é analisar a atividade de *code review* em repositórios populares do GitHub. Para isso, o projeto realiza consultas GraphQL para identificar **1.000 repositórios populares** (com >100 estrelas) e que possuam pelo menos **100 Pull Requests (PRs)**.

Em seguida, o script recolhe métricas detalhadas desses PRs para responder a um conjunto de **8 questões de pesquisa (RQs)** sobre os fatores que influenciam o *merge* e o processo de revisão.

Este conjunto de ficheiros permite tanto a **reprodução do estudo** quanto a **análise detalhada das métricas** obtidas.

---

## 🗂️ Estrutura do Repositório e Ficheiros

- **Program.cs**: Código-fonte principal em C# responsável pela seleção dos repositórios, recolha dos Pull Requests e exportação dos dados.
- **pull_requests.csv**: Ficheiro CSV contendo os dados brutos recolhidos dos PRs analisados.
- **Relatorio.pdf**: Relatório final com as hipóteses, metodologia, análise completa, discussão dos resultados e conclusões do estudo.

---

## ✨ Funcionalidades

- Recolha automática dos repositórios populares que atendem aos critérios (>= 100 PRs).
- Recolha de métricas de Pull Requests (MERGED ou CLOSED) via API GraphQL do GitHub.
- Filtragem de PRs para análise (>= 1 revisão e tempo de análise >= 1 hora).
- Exportação dos dados brutos para um ficheiro CSV (`pull_requests.csv`).

---

## 🧾 Colunas do CSV

- **Number**: Número de identificação do PR.
- **Title**: Título do PR.
- **BodyLength**: Número de caracteres no corpo da descrição do PR.
- **CreatedAt**: Data de criação.
- **ClosedAt**: Data de fecho ou *merge*.
- **ReviewTimeHours**: Tempo total de análise (em horas).
- **Additions**: Linhas adicionadas.
- **Deletions**: Linhas removidas.
- **ChangedFiles**: Ficheiros modificados.
- **Reviews**: Número de revisões.
- **Comments**: Número de comentários.
- **Participants**: Número de participantes.

---

## 🚀 Como Executar o Projeto

### Pré-requisitos

- [.NET SDK](https://dotnet.microsoft.com/) instalado.
- Um **Personal Access Token (PAT)** do GitHub com o escopo `public_repo`.

### Passos

1. **Clone o repositório:**

   ```bash
   git clone [https://github.com/](https://github.com/)[SEU_USUARIO]/[NOME_DO_REPOSITORIO]
   cd [NOME_DA_PASTA]
   ```

2. **Insira o seu Token de Acesso:**

   * Abra o ficheiro `Program.cs`.
   * Localize a linha:

     ```csharp
     private static readonly string GitHubToken = "";
     ```

   * Substitua `""` pelo seu token do GitHub.

3. **Execute o Script:**

   Navegue até ao diretório do projeto pelo terminal e execute:

         ```bash
         dotnet run
         ```
         
   Após a execução, o ficheiro `pull_requests.csv` será gerado no diretório raiz.

---

## 📈 Relatório Técnico (Resumo dos Resultados)

A análise dos dados, detalhada no `Relatorio.pdf`, investigou 8 Questões de Pesquisa (RQs) divididas em duas dimensões principais. As métricas foram analisadas estatisticamente (utilizando correlação de Spearman) para validar as hipóteses.

**Dimensão A: Feedback Final das Revisões (Status do PR)**

| Questão de Pesquisa (RQ) | Métrica(s) Analisada(s) |
| :--- | :--- |
| **RQ01** | **Tamanho:** Additions, Deletions, ChangedFiles |
| **RQ02** | **Tempo de Análise:** ReviewTimeHours |
| **RQ03** | **Descrição:** BodyLength |
| **RQ04** | **Interações:** Participants, Comments |

**Dimensão B: Número de Revisões**

| Questão de Pesquisa (RQ) | Métrica(s) Analisada(s) |
| :--- | :--- |
| **RQ05** | **Tamanho:** Additions, Deletions, ChangedFiles |
| **RQ06** | **Tempo de Análise:** ReviewTimeHours |
| **RQ07** | **Descrição:** BodyLength |
| **RQ08** | **Interações:** Participants, Comments |

O relatório completo com a discussão detalhada, incluindo os valores de correlação e a validação das hipóteses, pode ser encontrado no ficheiro `Relatorio.pdf`.

---

## 📝 Licença

Este projeto está sob a licença **MIT**. Veja o ficheiro `LICENSE.md` (se aplicável) para mais detalhes.

---

## 👥 Autores

- Gabriel Afonso Infante Vieira
- Rafael de Paiva Gomes
- Rafaella Cristina de Sousa Sacramento

---

<p align="center"><em>Projeto desenvolvido para a disciplina de Laboratório de Experimentação de Software - PUC Minas</em></p>
