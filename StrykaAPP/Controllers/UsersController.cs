using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using MySql.Data.MySqlClient;
using BCrypt.Net;

namespace StrykaAPP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public UsersController(DatabaseContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Endpoint para registrar um novo usuário.
        /// </summary>
        [HttpPost("register")]
        public IActionResult Register([FromBody] UserRegisterDto userDto)
        {
            try
            {
                // Validação dos campos
                if (string.IsNullOrWhiteSpace(userDto.Username) ||
                    string.IsNullOrWhiteSpace(userDto.Email) ||
                    string.IsNullOrWhiteSpace(userDto.Password))
                {
                    return BadRequest(new { message = "All fields are required." });
                }

                // Hash da senha
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password);

                using var connection = _context.GetConnection();
                connection.Open();

                // Verifica se o email já existe no banco de dados
                var checkEmailCommand = new MySqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", connection);
                checkEmailCommand.Parameters.AddWithValue("@Email", userDto.Email);
                var emailExists = Convert.ToInt32(checkEmailCommand.ExecuteScalar()) > 0;

                if (emailExists)
                {
                    return Conflict(new { message = "This email is already in use." });
                }

                // Insere o novo usuário no banco de dados
                var command = new MySqlCommand("INSERT INTO Users (Username, PasswordHash, Email) VALUES (@Username, @PasswordHash, @Email)", connection);
                command.Parameters.AddWithValue("@Username", userDto.Username);
                command.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                command.Parameters.AddWithValue("@Email", userDto.Email);

                var result = command.ExecuteNonQuery();

                // Retorna sucesso ou erro
                return result > 0
                    ? Ok(new { message = "User registered successfully!" })
                    : StatusCode(500, new { message = "Error registering user." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Endpoint para login de usuários.
        /// </summary>
        [HttpPost("login")]
        public IActionResult Login([FromBody] UserLoginDto loginDto)
        {
            try
            {
                if (string.IsNullOrEmpty(loginDto.Email) || string.IsNullOrEmpty(loginDto.Password))
                {
                    return BadRequest("Email and password are required.");
                }

                // Conecta ao banco e busca o usuário pelo email
                using var connection = _context.GetConnection();
                connection.Open();

                var command = new MySqlCommand("SELECT UserId, Username, Email, PasswordHash FROM Users WHERE Email = @Email", connection);
                command.Parameters.AddWithValue("@Email", loginDto.Email);

                using var reader = command.ExecuteReader();
                if (!reader.HasRows)
                {
                    return Unauthorized("Invalid email or password.");
                }

                // Lê os dados do usuário
                reader.Read();
                var user = new User
                {
                    UserId = reader.GetInt32("UserId"),
                    Username = reader.GetString("Username"),
                    Email = reader.GetString("Email"),
                    PasswordHash = reader.GetString("PasswordHash")
                };

                // Verifica a senha usando BCrypt
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);
                if (!isPasswordValid)
                {
                    return Unauthorized("Invalid email or password.");
                }

                // Retorna sucesso (aqui você pode incluir geração de JWT no futuro)
                return Ok(new
                {
                    message = "Login successful!",
                    user = new
                    {
                        user.UserId,
                        user.Username,
                        user.Email
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "An unexpected error occurred.");
            }
        }
    }

    /// <summary>
    /// DTO para login de usuários.
    /// </summary>
    public class UserLoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    /// <summary>
    /// DTO para registrar usuários.
    /// Apenas os dados necessários para o registro.
    /// </summary>
    public class UserRegisterDto
    {
        public string Username { get; set; }
        public string Password { get; set; } // A senha fornecida será hashada
        public string Email { get; set; }
    }

    /// <summary>
    /// Modelo da entidade User para persistência no banco de dados.
    /// </summary>
    public class User
    {
        public int UserId { get; set; } // Chave primária
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; } // Senha hashada
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
