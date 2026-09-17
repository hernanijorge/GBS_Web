using System.Data;
using Microsoft.AspNetCore.Identity;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

// Marker type for PasswordHasher<TUser> — this app has no other use for a
// "user" domain object, TBL_APP_USER is read/written directly by row.
public sealed class AppUser;

public class UserRepository
{
    private readonly string _connectionString;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    // Runs once at startup. If TBL_APP_USER is empty, seeds a single active
    // user with the given username/password (hashed, never stored in plain
    // text) so the app has someone to log in as on a fresh database.
    public async Task EnsureSeedUserAsync(string username, string password)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM TBL_APP_USER";
            var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            if (count > 0)
            {
                return;
            }
        }

        var hash = _hasher.HashPassword(new AppUser(), password);

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandType = CommandType.Text;
        insertCommand.BindByName = true;
        insertCommand.CommandText = @"
            INSERT INTO TBL_APP_USER (ID_USER, USERNAME, PASSWORD_HASH)
            VALUES (SEQ_APP_USER.NEXTVAL, :P_USERNAME, :P_HASH)";
        insertCommand.Parameters.Add(new OracleParameter("P_USERNAME", username));
        insertCommand.Parameters.Add(new OracleParameter("P_HASH", hash));
        await insertCommand.ExecuteNonQueryAsync();
    }

    // Validates credentials against TBL_APP_USER. Returns true only for an
    // active user whose password hash matches; updates LAST_LOGIN_AT on
    // success and transparently rehashes if the hasher's algorithm changed.
    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        int idUser;
        string storedHash;

        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandType = CommandType.Text;
            selectCommand.BindByName = true;
            selectCommand.CommandText = @"
                SELECT ID_USER, PASSWORD_HASH
                  FROM TBL_APP_USER
                 WHERE USERNAME = :P_USERNAME AND IS_ACTIVE = 'Y'";
            selectCommand.Parameters.Add(new OracleParameter("P_USERNAME", username));

            using var reader = await selectCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return false;
            }

            idUser = Convert.ToInt32(reader["ID_USER"]);
            storedHash = (string)reader["PASSWORD_HASH"];
        }

        var result = _hasher.VerifyHashedPassword(new AppUser(), storedHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return false;
        }

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.CommandType = CommandType.Text;
            updateCommand.BindByName = true;

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var newHash = _hasher.HashPassword(new AppUser(), password);
                updateCommand.CommandText = @"
                    UPDATE TBL_APP_USER
                       SET LAST_LOGIN_AT = SYSDATE, PASSWORD_HASH = :P_HASH
                     WHERE ID_USER = :P_ID";
                updateCommand.Parameters.Add(new OracleParameter("P_HASH", newHash));
            }
            else
            {
                updateCommand.CommandText = "UPDATE TBL_APP_USER SET LAST_LOGIN_AT = SYSDATE WHERE ID_USER = :P_ID";
            }

            updateCommand.Parameters.Add(new OracleParameter("P_ID", idUser));
            await updateCommand.ExecuteNonQueryAsync();
        }

        return true;
    }
}
