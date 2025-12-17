using Microsoft.AspNetCore.Mvc;
using SaleDetail.Api.Utils;

namespace SaleDetail.Api.Controllers;

/// <summary>
/// Controlador temporal para generar tokens JWT de desarrollo
/// SOLO PARA TESTING - Eliminar en producción
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Genera un token JWT válido para testing
    /// </summary>
    /// <param name="userId">ID del usuario (default: 1)</param>
    /// <param name="username">Nombre de usuario (default: "testuser")</param>
    /// <param name="role">Rol del usuario (default: "Admin")</param>
    /// <returns>Token JWT válido</returns>
    /// <remarks>
    /// SOLO USAR EN DESARROLLO
    /// 
    /// Ejemplo de petición:
    /// 
    ///     GET /api/Auth/generate-token?userId=1&amp;username=diego&amp;role=Admin
    ///     
    /// Respuesta:
    /// 
    ///     {
    ///       "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///       "expiresAt": "2025-12-17T15:30:00Z",
    ///       "howToUse": {
    ///         "swagger": "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///         "powershell": "$headers = @{\"Authorization\" = \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"}"
    ///       }
    ///     }
    /// </remarks>
    [HttpGet("generate-token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    public IActionResult GenerateToken(
        [FromQuery] int userId = 1,
        [FromQuery] string username = "testuser",
        [FromQuery] string role = "Admin")
    {
        var token = JwtTokenGenerator.GenerateToken(userId, username, role);
        
        return Ok(new TokenResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            HowToUse = new HowToUseToken
            {
                Swagger = $"Bearer {token}",
                Powershell = $"$headers = @{{\"Authorization\" = \"Bearer {token}\"}}"
            }
        });
    }
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public HowToUseToken HowToUse { get; set; } = new();
}

public class HowToUseToken
{
    public string Swagger { get; set; } = string.Empty;
    public string Powershell { get; set; } = string.Empty;
}
