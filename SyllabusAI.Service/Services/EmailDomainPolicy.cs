namespace SyllabusAI.Services;

/// <summary>
/// Girişe izin verilen e-posta alan adları:
/// Öğrenci: @bahcesehir.edu.tr | Eğitmen: @bau.edu.tr, @ou.bau.edu.tr
/// </summary>
public static class EmailDomainPolicy
{
    public static bool IsAllowedEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var e = email.Trim().ToLowerInvariant();
        return e.EndsWith("@bahcesehir.edu.tr")
               || e.EndsWith("@bau.edu.tr")
               || e.EndsWith("@ou.bau.edu.tr");
    }

    public const string DomainErrorMessage = "Sadece @bahcesehir.edu.tr (öğrenci), @bau.edu.tr ve @ou.bau.edu.tr (eğitmen) adresleri ile giriş yapılabilir.";
}
