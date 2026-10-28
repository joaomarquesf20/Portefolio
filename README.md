# 📸 Shots By Marques — Portefólio de Fotografia

Portefólio online para fotografia de **eventos e sessões de carros** com:
galerias com seleção de fotos, **pagamento por foto (Stripe)** e download após compra,
marcação de sessões com disponibilidade em tempo real, e painel de administração.

Projeto final de curso · ASP.NET Core (.NET 10) + EF Core + SQL Server LocalDB + Identity + Stripe.

---

## 🚀 Como correr (Windows + Visual Studio)

**Pré-requisitos:** Visual Studio 2026 (com .NET 10 SDK) e SQL Server Express **LocalDB** (vem incluído no VS).

1. Abre a solução `ShotsByMarques.sln` no Visual Studio
2. Primeira execução (automaticamente):
   - cria a base de dados `ShotsByMarques` no LocalDB
   - cria o admin e dados de exemplo (galerias, fotos, tipos de sessão)
3. Carrega em **F5** (ou `dotnet run` na pasta do projeto)

### Contas de demonstração

| Conta | Email | Palavra-passe | Permissões |
|---|---|---|---|
| Admin | `marquesjoao2006@gmail.com` | `Admin123!` | Painel de administração |
| Cliente | `cliente@shotsbymarques.pt` | `Cliente123!` | Compras, downloads, marcações |

### Contactos reais

- Email: `marquesphotography06@gmail.com`
- Instagram: `https://www.instagram.com/shots.by.marques/`

> Nota: a base de dados é gerida com **EF Core Migrations** — o arranque aplica as migrations pendentes
> (`MigrateAsync`) e cria a base de dados na primeira execução. Para evoluir o modelo:
> `dotnet ef migrations add <NomeDaMigration>` e depois `dotnet ef database update` (ou reinicia o site).

## 💳 Pagamentos com Stripe (cartão + MB Way)

1. Cria conta em **https://dashboard.stripe.com** → *Developers → API keys* → copia as chaves de **teste** (`sk_test_...` e `pk_test_...`)
2. Guarda a chave secreta em **user-secrets** (nunca no `appsettings.json` — fica fora do git). Na pasta
   `src/ShotsByMarques.Web`:
   ```powershell
   dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
   ```
   (a PublishableKey pode ficar em `appsettings.json`)
3. **MB Way**: ativa o método de pagamento MB Way no Dashboard do Stripe (*Settings → Payment methods* — requer conta Stripe em Portugal). No Checkout, o cliente escolhe cartão ou MB Way (telemóvel + aprovação na app).
   - Em modo de teste usa os números de telemóvel de teste indicados pela Stripe.
   - A confirmação do MB Way é **assíncrona**: o cliente volta à página de sucesso com estado *processamento* e as fotos ficam disponíveis assim que o pagamento é confirmado (webhook).
4. Corre o site e faz uma compra — serás redirecionado para o checkout da Stripe em modo teste.
   Cartão de teste: `4242 4242 4242 4242`, qualquer data futura, CVC `123`.
5. **Webhook (opcional, para confirmação em tempo real):** instala o Stripe CLI
   (https://docs.stripe.com/stripe-cli) e corre:
   ```
   stripe listen --forward-to localhost:5123/stripe/webhook
   ```
   Cola o `whsec_...` mostrado em `Stripe:WebhookSecret` e reinicia o site.
   Sem webhook, o pagamento é confirmado na página de sucesso (fallback).

> Alternativa sem Stripe: para MB Way direto (ifthenpay/Eupago) ou confirmação manual, ver notas abaixo.

## 🗂️ Estrutura do projeto

```
src/ShotsByMarques.Web/
├── Controllers/       # MVC + StripeWebhook (API)
├── Data/              # DbContext + Seeder
├── Models/            # Entidades (Gallery, Photo, Order, Booking, SessionType…)
├── Resources/         # Localização pt-PT (default) / en-US (SharedResource*.resx)
├── Services/          # BookingService, StripeCheckoutService, PhotoStorageService
├── ViewModels/
├── Views/             # Razor + Tailwind CSS + JavaScript vanilla
└── wwwroot/           # CSS, JS, uploads de fotos
```

## 🧩 Funcionalidades

- **Portefólio** público com galerias e lightbox (estilo Pixieset)
- **Seleção de fotos**: o cliente marca as favoritas (preview com marca de água) e vê o preço; botão **"Selecionar todas"** para escolher a galeria inteira de uma vez; o ícone do carrinho no menu leva as fotos selecionadas para o checkout. No checkout, o cliente pode **remover uma foto do carrinho** (✕ em cada item) ou **esvaziar o carrinho** por completo — o total e o carrinho atualizam-se em tempo real
- **Portefólio dividido**: na página inicial e no portefólio, as galerias aparecem em secções separadas **Sessões** e **Eventos** (estilo Pixieset)
- **Pagamento por foto** via Stripe Checkout (EUR); webhook confirma e liberta os downloads
- **Packs de fotos com desconto**: comprar 5+ fotos aplica desconto automático (ex.: Pack 5 Fotos −10%, Pack 10 −15%, Pack 20 −20% — configuráveis em `AdminPhotoPacks`)
- **Sessões**: apenas "Sessão de Carros" (45€) com marcação online
- **Sessões — fotos incluídas**: quando a sessão é paga, as fotos carregadas na galeria privada da marcação ficam **incluídas no preço** — o cliente descarrega-as sem pagar outra vez, em alta resolução e **sem marca de água** (secção "As minhas sessões" na página de Downloads, ou diretamente na galeria privada com "Descarregar todas")
- **Downloads** só para o cliente que pagou (ou é dono da sessão), em alta resolução e sem marca de água
- **Marcação de sessões**: tipos de sessão, **calendário de disponibilidade** no próprio formulário (dias coloridos: disponível/não/talvez/evento), horários disponíveis em tempo real, estado da marcação
- **Localização + deslocação**: ao marcar, o cliente escolhe a **zona/localidade** (lista de municípios portugueses com distâncias); dentro de **60 km da base** não há custo extra, além disso cobra-se **X €/km** (configurável em `appsettings.json` → `Travel`; base: **Ancede**). A distância e a taxa aparecem em tempo real antes de confirmar. A zona do dia aparece como **informação fixa** (ex.: "📍 Porto") — nunca como escolha; o cliente só escolhe a **localidade** dentro dessa zona. Em dias de **evento** a secção de localização nem aparece (só a localização do evento). Em dias sem zona marcada, o cliente escolhe zona + localidade livremente (deslocação). O **servidor rejeita** qualquer localidade fora da zona fixa do dia
- **Pagamento 50/50**: **50% no ato da marcação** (depósito, via Stripe cartão/MB Way) e **50% na entrega das fotos** (o cliente paga o saldo em "As minhas marcações" ou no "Downloads"; as fotos da sessão só são libertadas depois do saldo — o admin também pode marcar pagamentos recebidos em mão no painel)
- **Calendário de disponibilidade**: no admin, um **calendário mensal** onde o fotógrafo marca cada dia futuro — 🟢 Disponível, 🔴 Não disponível, 🟡 Talvez ou 🎪 Evento com mini sessões (limite configurável, ex. 4). Os dias sem marcação seguem a agenda semanal predefinida (**09:00–20:00** em todos os dias — o admin pode ajustar qualquer dia, incluindo **horários noturnos**, ex. 20:00–23:00); as marcações já feitas mantêm-se sempre. A **janela é o horário de trabalho**: as sessões têm de caber lá dentro, por isso numa janela 09:00–20:00 as sessões de 2h vão das **09:00 às 18:00** (a última acaba às 20:00) e as mini sessões (30 min) vão das 09:00 às **19:30** de 30 em 30 minutos. A grelha de horários mostra **o dia completo**, com as horas já ocupadas ou passadas **visíveis mas bloqueadas** (a cinzento) — assim percebe-se logo porque é que um dia tem menos horários livres. No dia atual há uma nota explicativa; clicar num dia de **evento seleciona-o e mostra os horários das mini sessões** para reservar ali mesmo (preço e depósito de mini sessão, localização do evento fixa); dias **não disponíveis** limpam a seleção e mostram porquê. A zona nunca é uma escolha num dia de evento
- **Mini sessões em eventos**: num dia marcado como Evento, o admin define também os **horários** das mini sessões (por defeito **09:00–20:00**, mas pode pôr horários noturnos, ex. 20:00–23:00); o cliente vê a secção "Mini sessões em eventos" na página Sessões, **escolhe um dos horários livres** e reserva com **vagas limitadas** (3/4 por evento) e **depósito de 50% no ato da marcação** (o saldo paga-se na entrega das fotos, como nas sessões normais). A reserva fica **ligada à localização do evento** (ex.: "Ponte de Lima") para o fotógrafo saber onde foi. As fotos da mini sessão são entregues como as de uma sessão normal (galeria privada, incluídas no preço, sem marca de água)
- **Autenticação** Identity com roles (Admin/Cliente) e conta demo
- **Dashboard admin**: galerias, upload de fotos, marcações, encomendas e receita
- **Galerias — privar em vez de eliminar**: no painel de galerias, cada galeria tem um botão **Privar** (esconde do público sem apagar; aparece **Publicar** para repor). Com as **caixas de seleção** o admin pode selecionar várias de uma vez (ou todas) e aplicar **Privar selecionadas** ou **Eliminar selecionadas** em massa
- **Tipo de galeria explícito**: ao criar/editar uma galeria o admin escolhe **Evento** (fotos vendidas à parte) ou **Sessão** (fotos incluídas no preço); o portefólio e os uploads respeitam essa escolha
- **Dar preço às fotos já carregadas**: se as fotos foram carregadas quando a galeria era Sessão (ficaram sem custo) e o admin depois a troca para Evento, a página de fotos mostra **"Aplicar este preço a todas"** — usa o mesmo campo de preço do upload, sem ter de carregar mais fotos. Também é possível **selecionar fotos individualmente** (caixas por foto + "Selecionar todas") e aplicar preço **só às selecionadas**, ou marcá-las sem preço
- **Página de fotos = gestão completa da galeria**: o tipo (Evento/Sessão), título, descrição, capa e publicar/privar editam-se no topo da página de fotos — o card no painel só tem **Fotos** (botão principal) e **Privar**
- **Painel admin — navegação e eliminação**: todas as páginas admin (Galerias, Marcações, Encomendas, Tipos de sessão, Disponibilidade, Packs) têm um link **"← Voltar ao painel"** no topo. Galerias, marcações, encomendas, tipos de sessão e packs têm **Eliminar individual** e **eliminar em massa** (caixas de seleção + "Eliminar selecionadas", com confirmação). Eliminar uma **marcação** apaga também a galeria privada de entrega dessa sessão (e as suas fotos); eliminar uma **encomenda** apaga os seus itens; tipos de sessão com marcações associadas são **desativados** em vez de apagados (a Mini Sessão — Evento nunca se apaga)
- **Galerias — distinção pública/oculta/privada**: cada galeria tem um badge colorido — **🌐 Pública** (verde, visível no portefólio), **👁 Ocultada** (escondida do público mas guardada) ou **🔒 Privada** (âmbar, entrega a um cliente) — e filtros no topo (**Todas / Públicas / Ocultas / Privadas**). O botão do card **👁 Ocultar** esconde do público (não confundir com **galeria privada**, que entrega as fotos a um cliente específico — isso configura-se na página de edição, com a marcação associada)
- **Idiomas**: português (PT-PT) por defeito + inglês (EN) com seletor na barra

## 🔒 Notas de produção

- **Proteção das fotos**: os uploads ficam **fora** da webroot (`App_Data/photos`) — nunca são servidos como ficheiros estáticos. Só o endpoint `DownloadsController.File` (após verificar dono + pagamento) os entrega. As prévias públicas passam por `Photos/Preview/{id}` e levam **marca de água + resolução reduzida** — mesmo que o cliente faça "Guardar imagem como…" ou screenshot, o que fica é a prévia com marca, nunca o original em alta resolução. O menu de contexto (clique direito) e o arrastar das fotos estão bloqueados na galeria/lightbox (medida dissuasora; screenshots não são evitáveis por nenhum site).
- **Entrega das fotos certas à pessoa certa**: cada sessão é uma **marcação** ligada à conta do cliente (`ClientUserId`). Quando a sessão acontece, o fotógrafo cria uma **galeria privada ligada a essa marcação** (`AdminGalleries` → marca a galeria como privada e escolhe a marcação). Só esse cliente vê e descarrega a galeria; o painel admin mostra sempre o nome/email do cliente na galeria e nas fotos. As encomendas e downloads ficam igualmente presos à conta do cliente.
- **Ao tornar privada, os preços somem**: quando uma galeria passa a privada (ligada a uma marcação), o site **apaga automaticamente o preço de todas as fotos** — a sessão já foi paga, nunca podem ser vendidas à parte. A página pública também **nunca mostra preço/botão de compra** em galerias de sessão, e o checkout bloqueia a compra dessas fotos (proteção dupla, mesmo para pedidos manipulados)
- **Sessões não pagam fotos duas vezes**: as galerias ligadas a marcações carregam as fotos como **incluídas no preço** (o upload força `forSale=false` automaticamente) — o cliente nunca volta a pagar pelas fotos da sessão. O download (`Downloads/SessionPhoto`) verifica a posse da marcação e entrega o original sem marca; fora do dono (ou admin) dá 404.
- **Marca de água**: no upload, o `PhotoStorageService` gera automaticamente uma prévia com marca de água (© Shots By Marques, diagonal, redimensionada para 1600px) usando **SkiaSharp**. O ficheiro original fica em `App_Data/photos` e só é servido após pagamento; a prévia com marca é o que os clientes veem na galeria e no checkout.
- **Fotos de exemplo**: o seed cria SVGs de demonstração; quando carregas as primeiras fotos reais de uma galeria, as de exemplo são removidas automaticamente (ou via botão "Remover fotos de exemplo" na página de fotos do admin).
- **Armazenamento em produção**: fotos originais em S3-compatible + cópias de segurança; o `PhotosController`/`DownloadsController` devem passar a ler de lá.
- **Emails**: confirmações de marcação/compra (SMTP ou Brevo)
- **Faturação**: precisas de NIF/empresa para faturar com Stripe em produção
- **HTTPS**: usa a opção `UseHttpsRedirection` já ativa fora de Development
- **Validação**: as mensagens de validação dos formulários estão em português (hardcoded nos data annotations); para EN completo, move-as para os `.resx`
- O site roda em `http://localhost:5123` (definido no `launchSettings.json`)
