namespace SyllabusAI.Services;

/// <summary>
/// Girişe izin verilen e-posta alan adları:
/// Öğrenci: @bahcesehir.edu.tr | Eğitmen: @bau.edu.tr, @ou.bau.edu.tr
/// </summary>
public static class EmailDomainPolicy
{
    private static readonly string[] StudentDomains = { "@bahcesehir.edu.tr" };
    private static readonly string[] InstructorDomains = { "@bau.edu.tr", "@ou.bau.edu.tr" };
    private static readonly HashSet<string> AllowedEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "ogrenci@bahcesehir.edu.tr",
        "egitmen@bau.edu.tr",
        "hoca@ou.bau.edu.tr"
    };

    /// <summary>
    /// E-posta bu domainlerden biriyle bitiyorsa rol adını döner, yoksa null (giriş yasak).
    /// </summary>
    public static string? GetRoleByEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        var e = email.Trim().ToLowerInvariant();
        if (e.EndsWith("@bahcesehir.edu.tr")) return "Student";
        if (e.EndsWith("@bau.edu.tr") || e.EndsWith("@ou.bau.edu.tr")) return "Instructor";
        return null;
    }

    public static bool IsAllowedEmail(string? email) => GetRoleByEmail(email) != null;
    public static bool IsAllowedSpecificEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return AllowedEmails.Contains(email.Trim());
    }

    public const string DomainErrorMessage = "Sadece @bahcesehir.edu.tr (öğrenci), @bau.edu.tr ve @ou.bau.edu.tr (eğitmen) adresleri ile giriş yapılabilir.";
    public const string EmailWhitelistErrorMessage = "Bu giriş ekranında sadece şu hesaplar aktiftir: ogrenci@bahcesehir.edu.tr, egitmen@bau.edu.tr, hoca@ou.bau.edu.tr";
}
