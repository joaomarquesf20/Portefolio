using System.Globalization;
using System.Text;

namespace ShotsByMarques.Web.Services;

/// <summary>
/// Lookup of Portuguese municipalities (approximate coordinates) used to
/// estimate the distance from the photographer's base for the travel fee.
/// Each city belongs to a <see cref="Region"/> so the booking form can group
/// the suggestions (e.g. "PORTO" with its municipalities underneath).
/// </summary>
public static class PortugalCities
{
    public record City(string Name, string Region, double Lat, double Lon, params string[] Aliases);

    public static readonly IReadOnlyList<City> All = new[]
    {
        // ---- Porto (capital + distrito) ----
        new City("Porto", "Porto", 41.1496, -8.6109),
        new City("Vila Nova de Gaia", "Porto", 41.1336, -8.6174, "Gaia"),
        new City("Matosinhos", "Porto", 41.1820, -8.6897),
        new City("Maia", "Porto", 41.2357, -8.6206),
        new City("Gondomar", "Porto", 41.1411, -8.5321),
        new City("Valongo", "Porto", 41.1950, -8.5092),
        new City("Paredes", "Porto", 41.2067, -8.3316),
        new City("Penafiel", "Porto", 41.2067, -8.2828),
        new City("Santo Tirso", "Porto", 41.3426, -8.4750),
        new City("Trofa", "Porto", 41.3373, -8.5553),
        new City("Vila do Conde", "Porto", 41.3519, -8.7475),
        new City("Póvoa de Varzim", "Porto", 41.3814, -8.7632),
        new City("Espinho", "Porto", 41.0074, -8.6410),
        new City("São João da Madeira", "Porto", 40.9009, -8.4902),
        new City("Oliveira de Azeméis", "Porto", 40.8404, -8.4756),
        new City("Vale de Cambra", "Porto", 40.8424, -8.3908),
        new City("Arouca", "Porto", 40.9305, -8.2456),
        new City("Castelo de Paiva", "Porto", 41.0471, -8.2622),
        new City("Marco de Canaveses", "Porto", 41.1847, -8.1481),
        new City("Ancede", "Porto", 41.1, -8.05),
        new City("Baião", "Porto", 41.1638, -8.0350),
        new City("Amarante", "Porto", 41.2732, -8.0822),
        new City("Felgueiras", "Porto", 41.3632, -8.1991),
        new City("Lousada", "Porto", 41.2784, -8.2842),
        new City("Paços de Ferreira", "Porto", 41.2768, -8.3770),

        // ---- Braga (distrito) ----
        new City("Braga", "Braga", 41.5454, -8.4265),
        new City("Guimarães", "Braga", 41.4422, -8.2911),
        new City("Barcelos", "Braga", 41.5388, -8.6150),
        new City("Vila Nova de Famalicão", "Braga", 41.4098, -8.5197, "Famalicão"),
        new City("Fafe", "Braga", 41.4510, -8.1708),
        new City("Vizela", "Braga", 41.3821, -8.2772),
        new City("Amares", "Braga", 41.6313, -8.3507),
        new City("Esposende", "Braga", 41.5350, -8.7810),
        new City("Póvoa de Lanhoso", "Braga", 41.5754, -8.2670),
        new City("Vieira do Minho", "Braga", 41.6316, -8.1428),
        new City("Terras de Bouro", "Braga", 41.7165, -8.2962),
        new City("Cabeceiras de Basto", "Braga", 41.5141, -8.0006),
        new City("Celorico de Basto", "Braga", 41.3863, -8.0006),
        new City("Mondim de Basto", "Braga", 41.4108, -7.9508),

        // ---- Viana do Castelo (distrito) ----
        new City("Viana do Castelo", "Viana do Castelo", 41.6932, -8.8329),
        new City("Monção", "Viana do Castelo", 42.0789, -8.4808),
        new City("Valença", "Viana do Castelo", 42.0320, -8.6340),
        new City("Caminha", "Viana do Castelo", 41.8751, -8.8371),
        new City("Ponte de Lima", "Viana do Castelo", 41.7677, -8.5839),
        new City("Arcos de Valdevez", "Viana do Castelo", 41.8465, -8.4189),
        new City("Ponte da Barca", "Viana do Castelo", 41.8081, -8.4173),

        // ---- Vila Real e Bragança (Trás-os-Montes) ----
        new City("Vila Real", "Vila Real e Bragança", 41.3005, -7.7448),
        new City("Chaves", "Vila Real e Bragança", 41.7405, -7.4711),
        new City("Alijó", "Vila Real e Bragança", 41.2763, -7.4727),
        new City("Sabrosa", "Vila Real e Bragança", 41.2668, -7.5760),
        new City("Peso da Régua", "Vila Real e Bragança", 41.1617, -7.7915),
        new City("Montalegre", "Vila Real e Bragança", 41.8230, -7.7900),
        new City("Boticas", "Vila Real e Bragança", 41.6890, -7.6700),
        new City("Ribeira de Pena", "Vila Real e Bragança", 41.5200, -7.7900),
        new City("Murça", "Vila Real e Bragança", 41.4080, -7.4540),
        new City("Bragança", "Vila Real e Bragança", 41.8065, -6.7572),
        new City("Mirandela", "Vila Real e Bragança", 41.4870, -7.1840),
        new City("Macedo de Cavaleiros", "Vila Real e Bragança", 41.5386, -6.9614),
        new City("Mogadouro", "Vila Real e Bragança", 41.3406, -6.7096),
        new City("Miranda do Douro", "Vila Real e Bragança", 41.4967, -6.2743),
        new City("Valpaços", "Vila Real e Bragança", 41.6076, -7.3105),
        new City("Vimioso", "Vila Real e Bragança", 41.5840, -6.5270),
        new City("Vinhais", "Vila Real e Bragança", 41.8350, -7.0050),

        // ---- Viseu (distrito) ----
        new City("Viseu", "Viseu", 40.6610, -7.9097),
        new City("Lamego", "Viseu", 41.0972, -7.8099),
        new City("Moimenta da Beira", "Viseu", 40.9834, -7.6115),
        new City("Mangualde", "Viseu", 40.6041, -7.7640),
        new City("Tondela", "Viseu", 40.5168, -8.0808),
        new City("Vouzela", "Viseu", 40.7241, -8.1113),
        new City("São Pedro do Sul", "Viseu", 40.7573, -8.0658),
        new City("Castro Daire", "Viseu", 40.8985, -7.9330),
        new City("Sátão", "Viseu", 40.7420, -7.7320),
        new City("Nelas", "Viseu", 40.5310, -7.8510),
        new City("Carregal do Sal", "Viseu", 40.4330, -8.0000),
        new City("Santa Comba Dão", "Viseu", 40.3980, -8.1320),
        new City("Cinfães", "Viseu", 41.0600, -8.0900),
        new City("Resende", "Viseu", 41.1060, -7.9500),
        new City("Armamar", "Viseu", 41.1080, -7.6900),
        new City("Tabuaço", "Viseu", 41.1160, -7.5700),
        new City("Tarouca", "Viseu", 41.0200, -7.7800),
        new City("Vila Nova de Paiva", "Viseu", 40.8500, -7.7300),
        new City("Penalva do Castelo", "Viseu", 40.6700, -7.6900),
        new City("Oliveira de Frades", "Viseu", 40.7300, -8.1800),

        // ---- Aveiro (distrito) ----
        new City("Aveiro", "Aveiro", 40.6405, -8.6538),
        new City("Santa Maria da Feira", "Aveiro", 40.9304, -8.5450),
        new City("Ovar", "Aveiro", 40.8591, -8.6260),
        new City("Ílhavo", "Aveiro", 40.6045, -8.6669),
        new City("Estarreja", "Aveiro", 40.7565, -8.5718),
        new City("Águeda", "Aveiro", 40.5694, -8.4455),
        new City("Anadia", "Aveiro", 40.4430, -8.4345),
        new City("Mealhada", "Aveiro", 40.3776, -8.4493),
        new City("Albergaria-a-Velha", "Aveiro", 40.6930, -8.4814),
        new City("Oliveira do Bairro", "Aveiro", 40.5140, -8.4930),
        new City("Murtosa", "Aveiro", 40.7360, -8.6380),
        new City("Sever do Vouga", "Aveiro", 40.7350, -8.3650),
        new City("Vagos", "Aveiro", 40.5550, -8.6810),

        // ---- Coimbra (distrito) ----
        new City("Coimbra", "Coimbra", 40.2033, -8.4103),
        new City("Cantanhede", "Coimbra", 40.3464, -8.5940),
        new City("Figueira da Foz", "Coimbra", 40.1482, -8.8552),
        new City("Soure", "Coimbra", 40.0592, -8.6265),
        new City("Arganil", "Coimbra", 40.2176, -8.0543),
        new City("Oliveira do Hospital", "Coimbra", 40.3610, -7.8610),
        new City("Lousã", "Coimbra", 40.1110, -8.2460),
        new City("Miranda do Corvo", "Coimbra", 40.0930, -8.3320),
        new City("Penacova", "Coimbra", 40.2680, -8.2830),
        new City("Condeixa-a-Nova", "Coimbra", 40.1110, -8.5000),
        new City("Montemor-o-Velho", "Coimbra", 40.1720, -8.6820),
        new City("Mira", "Coimbra", 40.4280, -8.7360),

        // ---- Guarda (distrito) ----
        new City("Guarda", "Guarda", 40.5373, -7.2655),
        new City("Seia", "Guarda", 40.4218, -7.7005),
        new City("Trancoso", "Guarda", 40.7832, -7.3501),
        new City("Sabugal", "Guarda", 40.3510, -7.0896),
        new City("Almeida", "Guarda", 40.7242, -6.9062),
        new City("Gouveia", "Guarda", 40.4930, -7.5920),
        new City("Manteigas", "Guarda", 40.4020, -7.5380),
        new City("Pinhel", "Guarda", 40.7730, -7.0620),
        new City("Celorico da Beira", "Guarda", 40.6370, -7.3860),
        new City("Figueira de Castelo Rodrigo", "Guarda", 40.8910, -6.9620),
        new City("Meda", "Guarda", 40.9650, -7.2600),
        new City("Fornos de Algodres", "Guarda", 40.6200, -7.4600),

        // ---- Castelo Branco (distrito) ----
        new City("Castelo Branco", "Castelo Branco", 39.8222, -7.4919),
        new City("Covilhã", "Castelo Branco", 40.2811, -7.5038),
        new City("Fundão", "Castelo Branco", 40.1402, -7.5014),
        new City("Idanha-a-Nova", "Castelo Branco", 39.9200, -7.2400),
        new City("Penamacor", "Castelo Branco", 40.1670, -7.1710),
        new City("Sertã", "Castelo Branco", 39.8000, -8.1000),
        new City("Proença-a-Nova", "Castelo Branco", 39.7500, -7.9200),
        new City("Oleiros", "Castelo Branco", 39.9200, -7.9100),
        new City("Vila Velha de Ródão", "Castelo Branco", 39.6500, -7.6700),
        new City("Vila de Rei", "Castelo Branco", 39.6700, -8.1400),

        // ---- Leiria (distrito) ----
        new City("Leiria", "Leiria", 39.7436, -8.8070),
        new City("Pombal", "Leiria", 39.9139, -8.6287),
        new City("Marinha Grande", "Leiria", 39.7470, -8.9320),
        new City("Alcobaça", "Leiria", 39.5502, -8.9775),
        new City("Caldas da Rainha", "Leiria", 39.4070, -9.1359),
        new City("Óbidos", "Leiria", 39.3605, -9.1574),
        new City("Peniche", "Leiria", 39.3537, -9.3812),
        new City("Batalha", "Leiria", 39.6580, -8.8250),
        new City("Porto de Mós", "Leiria", 39.6000, -8.8200),
        new City("Ansião", "Leiria", 39.9100, -8.4300),
        new City("Alvaiázere", "Leiria", 39.8200, -8.3800),
        new City("Figueiró dos Vinhos", "Leiria", 39.9000, -8.2700),
        new City("Pedrógão Grande", "Leiria", 39.9200, -8.1400),
        new City("Castanheira de Pêra", "Leiria", 40.0000, -8.2000),

        // ---- Santarém (distrito) ----
        new City("Santarém", "Santarém", 39.2362, -8.6851),
        new City("Tomar", "Santarém", 39.6040, -8.4081),
        new City("Fátima", "Santarém", 39.6306, -8.6732, "Ourém"),
        new City("Abrantes", "Santarém", 39.4628, -8.1973),
        new City("Cartaxo", "Santarém", 39.1595, -8.7875),
        new City("Almeirim", "Santarém", 39.2090, -8.6250),
        new City("Torres Novas", "Santarém", 39.4810, -8.5390),
        new City("Entroncamento", "Santarém", 39.4637, -8.4743),
        new City("Rio Maior", "Santarém", 39.3379, -8.9379),
        new City("Alcanena", "Santarém", 39.4590, -8.6690),
        new City("Coruche", "Santarém", 38.9598, -8.5284),
        new City("Chamusca", "Santarém", 39.3540, -8.4800),
        new City("Alpiarça", "Santarém", 39.2580, -8.5750),
        new City("Benavente", "Santarém", 38.9800, -8.8100),
        new City("Salvaterra de Magos", "Santarém", 39.0280, -8.7930),

        // ---- Lisboa (capital + distrito) ----
        new City("Lisboa", "Lisboa", 38.7223, -9.1393),
        new City("Mafra", "Lisboa", 38.9369, -9.3267),
        new City("Sintra", "Lisboa", 38.7997, -9.3880),
        new City("Cascais", "Lisboa", 38.6971, -9.4215),
        new City("Oeiras", "Lisboa", 38.6841, -9.2991),
        new City("Amadora", "Lisboa", 38.7605, -9.2318),
        new City("Loures", "Lisboa", 38.8308, -9.1685),
        new City("Odivelas", "Lisboa", 38.7911, -9.1888),
        new City("Vila Franca de Xira", "Lisboa", 38.9551, -8.9905),
        new City("Torres Vedras", "Lisboa", 39.0912, -9.2587),
        new City("Lourinhã", "Lisboa", 39.2420, -9.3120),
        new City("Cadaval", "Lisboa", 39.2400, -9.1000),
        new City("Sobral de Monte Agraço", "Lisboa", 38.9000, -9.1900),
        new City("Arruda dos Vinhos", "Lisboa", 38.9800, -9.0800),
        new City("Alenquer", "Lisboa", 39.0500, -9.0100),
        new City("Azambuja", "Lisboa", 39.0700, -8.8700),

        // ---- Setúbal (distrito) ----
        new City("Setúbal", "Setúbal", 38.5244, -8.8926),
        new City("Almada", "Setúbal", 38.6762, -9.1570),
        new City("Seixal", "Setúbal", 38.6429, -9.1079),
        new City("Barreiro", "Setúbal", 38.6600, -9.0746),
        new City("Montijo", "Setúbal", 38.7073, -8.9745),
        new City("Palmela", "Setúbal", 38.5886, -8.9007),
        new City("Sesimbra", "Setúbal", 38.4438, -9.1030),
        new City("Alcochete", "Setúbal", 38.7564, -8.9614),
        new City("Alcácer do Sal", "Setúbal", 38.3732, -8.5142),
        new City("Grândola", "Setúbal", 38.1770, -8.5660),
        new City("Santiago do Cacém", "Setúbal", 38.0163, -8.6954),
        new City("Sines", "Setúbal", 37.9557, -8.8685),
        new City("Moita", "Setúbal", 38.6500, -8.9900),

        // ---- Portalegre (distrito, Alentejo Norte) ----
        new City("Portalegre", "Portalegre", 39.2921, -7.4327),
        new City("Elvas", "Portalegre", 38.8794, -7.1624),
        new City("Ponte de Sor", "Portalegre", 39.2506, -8.0098),
        new City("Campo Maior", "Portalegre", 39.0100, -7.0700),
        new City("Nisa", "Portalegre", 39.5100, -7.6500),
        new City("Crato", "Portalegre", 39.2800, -7.6400),
        new City("Castelo de Vide", "Portalegre", 39.4100, -7.4500),
        new City("Marvão", "Portalegre", 39.3900, -7.3800),
        new City("Gavião", "Portalegre", 39.4600, -7.9300),
        new City("Avis", "Portalegre", 39.0500, -7.8900),
        new City("Alter do Chão", "Portalegre", 39.2000, -7.6500),
        new City("Fronteira", "Portalegre", 39.0600, -7.6500),
        new City("Monforte", "Portalegre", 39.0500, -7.4400),
        new City("Arronches", "Portalegre", 39.1200, -7.2800),

        // ---- Évora (distrito, Alentejo Central) ----
        new City("Évora", "Évora", 38.5715, -7.9096),
        new City("Estremoz", "Évora", 38.8424, -7.5856),
        new City("Montemor-o-Novo", "Évora", 38.6483, -8.2140),
        new City("Reguengos de Monsaraz", "Évora", 38.4240, -7.5348),
        new City("Vendas Novas", "Évora", 38.6764, -8.4560),
        new City("Évora Monte", "Évora", 38.7636, -7.7190),
        new City("Redondo", "Évora", 38.6400, -7.5400),
        new City("Vila Viçosa", "Évora", 38.7800, -7.4100),
        new City("Borba", "Évora", 38.8000, -7.4500),
        new City("Arraiolos", "Évora", 38.7200, -7.9800),
        new City("Viana do Alentejo", "Évora", 38.3300, -8.0000),
        new City("Portel", "Évora", 38.3000, -7.7000),

        // ---- Beja (distrito, Baixo Alentejo) ----
        new City("Beja", "Beja", 38.0151, -7.8632),
        new City("Mértola", "Beja", 37.6414, -7.6633),
        new City("Serpa", "Beja", 37.9454, -7.5973),
        new City("Moura", "Beja", 38.1400, -7.4475),
        new City("Odemira", "Beja", 37.5974, -8.6415),
        new City("Aljustrel", "Beja", 37.8772, -8.1653),
        new City("Castro Verde", "Beja", 37.6977, -8.0841),
        new City("Alvito", "Beja", 38.2500, -7.9900),
        new City("Vidigueira", "Beja", 38.2100, -7.8000),
        new City("Cuba", "Beja", 38.1600, -7.8900),
        new City("Ferreira do Alentejo", "Beja", 38.0600, -8.1100),
        new City("Ourique", "Beja", 37.6500, -8.2200),
        new City("Barrancos", "Beja", 38.1300, -6.9800),

        // ---- Faro (distrito, Algarve) ----
        new City("Faro", "Faro", 37.0194, -7.9304),
        new City("Silves", "Faro", 37.1891, -8.4385),
        new City("Monchique", "Faro", 37.3170, -8.5555),
        new City("Lagoa", "Faro", 37.1353, -8.4537),
        new City("Portimão", "Faro", 37.1369, -8.5377),
        new City("Lagos", "Faro", 37.1024, -8.6742),
        new City("Albufeira", "Faro", 37.0892, -8.2504),
        new City("Loulé", "Faro", 37.1378, -8.0238),
        new City("Olhão", "Faro", 37.0280, -7.8412),
        new City("Tavira", "Faro", 37.1263, -7.6483),
        new City("Vila Real de Santo António", "Faro", 37.1918, -7.4153),
        new City("Aljezur", "Faro", 37.3182, -8.8031),
        new City("Quarteira", "Faro", 37.0710, -8.1020),
        new City("Vilamoura", "Faro", 37.0775, -8.1130),
        new City("Castro Marim", "Faro", 37.2200, -7.4400),
        new City("Alcoutim", "Faro", 37.4700, -7.4700),
        new City("São Brás de Alportel", "Faro", 37.1500, -7.8900),

        // ---- Madeira ----
        new City("Funchal", "Madeira", 32.6669, -16.9241),
        new City("Câmara de Lobos", "Madeira", 32.6500, -16.9770),
        new City("Ribeira Brava", "Madeira", 32.6710, -17.0640),
        new City("Calheta", "Madeira", 32.7200, -17.1700),
        new City("Ponta do Sol", "Madeira", 32.6800, -17.1000),
        new City("Santa Cruz", "Madeira", 32.6880, -16.7910),
        new City("Machico", "Madeira", 32.7200, -16.7700),
        new City("Santana", "Madeira", 32.8000, -16.8800),

        // ---- Açores ----
        new City("Ponta Delgada", "Açores", 37.7394, -25.6687),
        new City("Angra do Heroísmo", "Açores", 38.6550, -27.2170),
        new City("Horta", "Açores", 38.5370, -28.6310),
        new City("Ribeira Grande", "Açores", 37.8200, -25.5200),
        new City("Lagoa (Açores)", "Açores", 37.7460, -25.5800),
        new City("Vila Franca do Campo", "Açores", 37.7200, -25.4300)
    };

    /// <summary>Finds a city by name (accents/case-insensitive, also matches aliases).</summary>
    public static City? TryFind(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var q = Normalize(query);
        return All.FirstOrDefault(c =>
            Normalize(c.Name) == q || c.Aliases.Any(a => Normalize(a) == q));
    }

    /// <summary>Haversine distance between two points in kilometres.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Lowercases and strips diacritics (é → e), for forgiving matching.</summary>
    public static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().ToLowerInvariant().Trim();
    }
}
