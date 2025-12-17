using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SaleDetail.Api.Utils;

/// <summary>
/// Generador de tokens JWT para desarrollo/testing
/// SOLO USAR PARA PRUEBAS - En producción el token viene del microservicio de autenticación
/// </summary>
public static class JwtTokenGenerator
{
    /// <summary>
    /// Genera un token JWT válido para testing
    /// </summary>
    /// <param name="userId">ID del usuario (default: 1)</param>
    /// <param name="username">Nombre de usuario (default: "testuser")</param>
    /// <param name="role">Rol del usuario (default: "Admin")</param>
    /// <returns>Token JWT válido</returns>
    public static string GenerateToken(
        int userId = 1, 
        string username = "testuser", 
        string role = "Admin")
    {
        // IMPORTANTE: Estos valores deben coincidir EXACTAMENTE con appsettings.json
        var key = "eLlO3LhzXDvWFxiMBsmg2zCir49SRD3xCdh2IfuptfI=";
        var issuer = "FarmaArquiSoft";
        var audience = "FarmaArquiSoftClients";
        var expiresMinutes = 60;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Genera un token JWT y lo muestra en consola con información útil
    /// </summary>
    public static void GenerateAndPrintToken()
    {
        var token = GenerateToken();
        
        Console.WriteLine("\n========================================");
        Console.WriteLine("TOKEN JWT GENERADO PARA TESTING");
        Console.WriteLine("========================================\n");
        Console.WriteLine("Token completo:");
        Console.WriteLine(token);
        Console.WriteLine("\n========================================");
        Console.WriteLine("Para usar en Swagger:");
        Console.WriteLine($"Bearer {token}");
        Console.WriteLine("\n========================================");
        Console.WriteLine("Para usar en PowerShell:");
        Console.WriteLine($"$headers = @{{\"Authorization\" = \"Bearer {token}\"}}");
        Console.WriteLine("========================================\n");
        
        // Decodificar y mostrar claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        Console.WriteLine("Claims incluidos:");
        foreach (var claim in jwtToken.Claims)
        {
            Console.WriteLine($"  {claim.Type}: {claim.Value}");
        }
        Console.WriteLine($"\nExpira: {jwtToken.ValidTo.ToLocalTime()}");
        Console.WriteLine("========================================\n");
    }
}
