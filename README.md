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
| Admin | `admin@shotsbymarques.pt` | `Admin123!` | Painel de administração |
| Cliente | `cliente@shotsbymarques.pt` | `Cliente123!` | Compras, downloads, marcações |

### Contactos reais

- Email: `marquesphotography06@gmail.com`
- Instagram: `https://www.instagram.com/shots.by.marques/`

> Nota: a base de dados é criada com `EnsureCreatedAsync` no arranque (bom para o scaffold).
> Para produção, usa **Migrations**: `dotnet ef migrations add InitialCreate` e depois `dotnet ef database update`.

## 💳 Pagamentos com Stripe (cartão + MB Way)

1. Cria conta em **https://dashboard.stripe.com** → *Developers → API keys* → copia as chaves de **teste** (`sk_test_...` e `pk_test_...`)
2. Cola-as em `appsettings.json`:
   ```json
   "Stripe": {
     "SecretKey": "sk_test_...",
     "PublishableKey": "pk_test_...",
     "WebhookSecret": "",
     "PaymentMethods": "card,mb_way"
   }
   ```
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
- **Seleção de fotos**: o cliente marca as favoritas (preview com marca de água) e vê o preço; botão **"Selecionar todas"** para escolher a galeria inteira de uma vez; o ícone do carrinho no menu leva as fotos selecionadas para o checkout
- **Portefólio dividido**: na página inicial e no portefólio, as galerias aparecem em secções separadas **Sessões** e **Eventos** (estilo Pixieset)
- **Pagamento por foto** via Stripe Checkout (EUR); webhook confirma e liberta os downloads
- **Packs de fotos com desconto**: comprar 5+ fotos aplica desconto automático (ex.: Pack 5 Fotos −10%, Pack 10 −15%, Pack 20 −20% — configuráveis em `AdminPhotoPacks`)
- **Sessões**: apenas "Sessão de Carros" (45€) com marcação online
- **Sessões — fotos incluídas**: quando a sessão é paga, as fotos carregadas na galeria privada da marcação ficam **incluídas no preço** — o cliente descarrega-as sem pagar outra vez, em alta resolução e **sem marca de água** (secção "As minhas sessões" na página de Downloads, ou diretamente na galeria privada com "Descarregar todas")
- **Downloads** só para o cliente que pagou (ou é dono da sessão), em alta resolução e sem marca de água
- **Marcação de sessões**: tipos de sessão, calendário com horários disponíveis, estado da marcação
- **Autenticação** Identity com roles (Admin/Cliente) e conta demo
- **Dashboard admin**: galerias, upload de fotos, marcações, encomendas e receita
- **Tipo de galeria explícito**: ao criar/editar uma galeria o admin escolhe **Evento** (fotos vendidas à parte) ou **Sessão** (fotos incluídas no preço); o portefólio e os uploads respeitam essa escolha
- **Idiomas**: português (PT-PT) por defeito + inglês (EN) com seletor na barra

## 🔒 Notas de produção

- **Proteção das fotos**: os uploads ficam **fora** da webroot (`App_Data/photos`) — nunca são servidos como ficheiros estáticos. Só o endpoint `DownloadsController.File` (após verificar dono + pagamento) os entrega. As prévias públicas passam por `Photos/Preview/{id}` e levam **marca de água + resolução reduzida** — mesmo que o cliente faça "Guardar imagem como…" ou screenshot, o que fica é a prévia com marca, nunca o original em alta resolução. O menu de contexto (clique direito) e o arrastar das fotos estão bloqueados na galeria/lightbox (medida dissuasora; screenshots não são evitáveis por nenhum site).
- **Entrega das fotos certas à pessoa certa**: cada sessão é uma **marcação** ligada à conta do cliente (`ClientUserId`). Quando a sessão acontece, o fotógrafo cria uma **galeria privada ligada a essa marcação** (`AdminGalleries` → marca a galeria como privada e escolhe a marcação). Só esse cliente vê e descarrega a galeria; o painel admin mostra sempre o nome/email do cliente na galeria e nas fotos. As encomendas e downloads ficam igualmente presos à conta do cliente.
- **Sessões não pagam fotos duas vezes**: as galerias ligadas a marcações carregam as fotos como **incluídas no preço** (o upload força `forSale=false` automaticamente) — o cliente nunca volta a pagar pelas fotos da sessão. O download (`Downloads/SessionPhoto`) verifica a posse da marcação e entrega o original sem marca; fora do dono (ou admin) dá 404.
- **Marca de água**: no upload, o `PhotoStorageService` gera automaticamente uma prévia com marca de água (© Shots By Marques, diagonal, redimensionada para 1600px) usando **SkiaSharp**. O ficheiro original fica em `App_Data/photos` e só é servido após pagamento; a prévia com marca é o que os clientes veem na galeria e no checkout.
- **Fotos de exemplo**: o seed cria SVGs de demonstração; quando carregas as primeiras fotos reais de uma galeria, as de exemplo são removidas automaticamente (ou via botão "Remover fotos de exemplo" na página de fotos do admin).
- **Armazenamento em produção**: fotos originais em S3-compatible + cópias de segurança; o `PhotosController`/`DownloadsController` devem passar a ler de lá.
- **Emails**: confirmações de marcação/compra (SMTP ou Brevo)
- **Faturação**: precisas de NIF/empresa para faturar com Stripe em produção
- **HTTPS**: usa a opção `UseHttpsRedirection` já ativa fora de Development
- **Validação**: as mensagens de validação dos formulários estão em português (hardcoded nos data annotations); para EN completo, move-as para os `.resx`
- O site roda em `http://localhost:5123` (definido no `launchSettings.json`)
