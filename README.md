# Análise de Repositórios Populares no GitHub (LAB01)

<p align="center">
  <img src="https://img.shields.io/badge/Linguagem-C%23-blueviolet" alt="Linguagem C#">
  <img src="https://img.shields.io/badge/Plataforma-.NET-blue" alt="Plataforma .NET">
  <img src="https://img.shields.io/badge/API-GraphQL-e10098" alt="API GraphQL">
</p>

## 📖 Descrição do Projeto

Este repositório contém o código e os artefatos desenvolvidos em C# como parte do **LAB01** da disciplina *Laboratório de Experimentação de Software*.

O objetivo principal é realizar consultas GraphQL para recolher informações de até **1.000 repositórios do GitHub**, extraindo todas as métricas necessárias para responder a um conjunto de **questões de pesquisa (RQs)** sobre as características de projetos open-source populares.

Este conjunto de ficheiros permite tanto a **reprodução do estudo** quanto a **análise detalhada das métricas** obtidas.

---

## 🗂️ Estrutura do Repositório e Ficheiros

- **Program.cs**: Código-fonte principal em C# responsável pela recolha, processamento e geração dos gráficos.
- **repositorios_populares.csv**: Ficheiro CSV contendo os dados brutos recolhidos dos 1.000 repositórios.
- **PlanilhaDeCalculos.xlsx**: Folha de cálculo com as fórmulas utilizadas para calcular as medianas e outras métricas sumarizadas.
- **Relatorio.pdf**: Relatório final com a análise completa, discussão dos resultados e conclusões do estudo.
- **\*.png**: Imagens dos gráficos gerados pelo script para cada questão de pesquisa.

---

## ✨ Funcionalidades

- Recolha automática de métricas via API GraphQL do GitHub.
- Exportação dos dados para um ficheiro CSV (`repositorios_populares.csv`).
- Geração automática de gráficos de barras com a biblioteca **ScottPlot** para visualização dos resultados.

---

## 🧾 Colunas do CSV

- **Nome**
- **Descrição**
- **URL**
- **Stars**
- **Forks**
- **Criado Em**
- **Última Atualização**
- **Linguagem**
- **Issues Totais**
- **Issues Fechadas**
- **Pull Requests**
- **Releases**

---

## 🚀 Como Executar o Projeto

### Pré-requisitos

- [.NET SDK](https://dotnet.microsoft.com/) instalado.
- Um **Personal Access Token (PAT)** do GitHub com o escopo `public_repo`.

### Passos

1. **Clone o repositório:**

   ```bash
   git clone https://github.com/RafaellaCristinaCSS/Laborat-rio-de-Experimenta-o-de-Software
   cd [NOME_DA_PASTA]

2. **Insira o seu Token de Acesso:**

   * Abra o ficheiro `Program.cs`.

   * Localize a linha:

     ```csharp
     string githubToken = "inserir_token_aqui";
     ```

   * Substitua `"inserir_token_aqui"` pelo seu token do GitHub.

3. **Execute o Script:**

   Navegue até ao diretório do projeto pelo terminal e execute:

         dotnet run
         

   Após a execução, os ficheiros `repositorios_populares.csv` e as imagens dos gráficos serão gerados no diretório raiz.

---


## 📈 Relatório Técnico (Resumo dos Resultados)

A análise dos dados, baseada em valores de **mediana**, revelou o seguinte perfil para um repositório popular típico:

| Questão de Pesquisa (RQ)        | Métrica                                  | Resultado (Mediana) |
|----------------------------------|-------------------------------------------|----------------------|
| **RQ01: Maturidade**             | Idade do Repositório (anos)              | 8                    |
| **RQ02: Contribuição Externa**   | Nº de Pull Requests Aceites              | 1.147                |
| **RQ03: Frequência de Releases** | Nº Total de Releases                     | 35,5                 |
| **RQ04: Atualizações**           | Tempo desde a Última Atualização (dias)  | Baixo¹               |
| **RQ05: Linguagens**             | Linguagem Predominante                   | Python               |
| **RQ06: Gestão de Issues**       | Razão de Issues Fechadas / Totais        | 0.86²                |

> ¹ A maioria dos projetos é atualizada numa questão de dias.  
> ² Cerca de 86% das issues são concluidas.  

O relatório completo com a discussão detalhada pode ser encontrado no ficheiro `Relatorio.pdf`.


---

## 📝 Licença

Este projeto está sob a licença **MIT**. Veja o ficheiro `LICENSE.md` para mais detalhes.

---

## 👥 Autores

* Gabriel Afonso Infante Vieira
* Rafael de Paiva Gomes
* Rafaella Cristina de Sousa Sacramento

---

<p align="center"><em>Projeto desenvolvido para a disciplina de Laboratório de Experimentação de Software - PUC Minas</em></p>
