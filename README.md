Análise de Repositórios Populares no GitHub (LAB01)
<p align="center">
<img src="https://www.google.com/search?q=https://img.shields.io/badge/Linguagem-C%2523-blueviolet" alt="Linguagem C#">
<img src="https://www.google.com/search?q=https://img.shields.io/badge/Plataforma-.NET-blue" alt="Plataforma .NET">
<img src="https://www.google.com/search?q=https://img.shields.io/badge/API-GraphQL-e10098" alt="API GraphQL">
</p>

📖 Descrição do Projeto
Este repositório contém o código e os artefatos desenvolvidos em C# como parte do LAB01 da disciplina Laboratório de Experimentação de Software.

O objetivo principal é realizar consultas GraphQL para coletar informações de até 1.000 repositórios do GitHub, extraindo todas as métricas necessárias para responder a um conjunto de questões de pesquisa (RQs) sobre as características de projetos open-source populares.

Este conjunto de arquivos permite tanto a reprodução do estudo quanto a análise detalhada das métricas obtidas.

🗂️ Estrutura do Repositório e Arquivos
Program.cs: Código-fonte principal em C# responsável pela coleta, processamento e geração dos gráficos.

repositorios_populares.csv: Arquivo CSV contendo os dados brutos coletados dos 1.000 repositórios.

PlanilhaDeCalculos.xlsx: Planilha com as fórmulas utilizadas para calcular as medianas e outras métricas sumarizadas.

Relatorio.pdf: Relatório final com a análise completa, discussão dos resultados e conclusões do estudo.

*.png: Imagens dos gráficos gerados pelo script para cada questão de pesquisa.

✨ Funcionalidades
Coleta automática de métricas via API GraphQL do GitHub.

Exportação dos dados para um arquivo CSV (repositorios_populares.csv).

Geração automática de gráficos de barras com a biblioteca ScottPlot para visualização dos resultados.

Colunas do CSV
Nome

Descrição

URL

Stars

Forks

Criado Em

Última Atualização

Linguagem

Issues Totais

Issues Fechadas

Pull Requests

Releases

🚀 Como Executar o Projeto
Pré-requisitos
.NET SDK instalado.

Um token de acesso pessoal (Personal Access Token) do GitHub com escopo public_repo.

Passos
Clone o repositório:

git clone [URL_DO_SEU_REPOSITORIO]
cd [NOME_DA_PASTA]


Insira seu Token de Acesso:

Abra o arquivo Program.cs.

Localize a linha: string githubToken = "inserir_token_aqui";

Substitua "inserir_token_aqui" pelo seu token do GitHub.

Execute o Script:

Navegue até o diretório do projeto pelo terminal e execute o comando:

dotnet run


Após a execução, o arquivo repositorios_populares.csv e as imagens dos gráficos serão gerados no diretório raiz.

📈 Relatório Técnico (Resumo dos Resultados)
A análise dos dados, baseada em valores de mediana, revelou o seguinte perfil para um repositório popular típico:

Questão de Pesquisa (RQ)

Métrica

Resultado (Mediana)

RQ01: Maturidade

Idade do Repositório (anos)

8

RQ02: Contribuição Externa

Nº de Pull Requests Aceitos

1.147

RQ03: Frequência de Releases

Nº Total de Releases

35,5

RQ04: Frequência de Atualizações

Tempo desde a Última Atualização (dias)

Baixo¹

RQ05: Linguagens

Linguagem Predominante

Python

RQ06: Gestão de Issues

Razão de Issues Fechadas / Totais

ainda n sei

¹A maioria dos projetos é atualizada em questão de dias.

O relatório completo com a discussão detalhada de cada ponto pode ser encontrado no arquivo Relatorio.pdf.

📝 Licença
Este projeto está sob a licença MIT. Veja o arquivo LICENSE.md para mais detalhes.

👥 Autores
Gabriel Afonso Infante Vieira

Rafael de Paiva Gomes

Rafaella Cristina de Sousa Sacramento

<p align="center">---</p>
<p align="center"><em>Projeto desenvolvido para a disciplina de Laboratório de Experimentação de Software - PUC Minas</em></p>
