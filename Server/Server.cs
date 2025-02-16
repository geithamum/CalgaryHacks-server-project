using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public partial class Server : Node
{
    private ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
    private Dictionary<string, string> userDatabase = new Dictionary<string, string>(); // Stores username & hashed password
    private Dictionary<string, Vector2> playerPositions = new Dictionary<string, Vector2>(); // Tracks last known positions
    private HashSet<string> loggedInUsers = new HashSet<string>(); // Tracks logged-in players

    private string userDatabasePath = "user_database.json";
    private string playerPositionsPath = "player_positions.json";

    public override void _Ready()
    {
        LoadUserDatabase();
        LoadPlayerPositions();
        StartServer();
    }

    private void StartServer()
    {
        peer.CreateServer(1234, 10);
        Multiplayer.MultiplayerPeer = peer;
        GD.Print("🖥️ Server started on port 1234");
    }

    private void LoadUserDatabase()
    {
        if (File.Exists(userDatabasePath))
        {
            string jsonText = File.ReadAllText(userDatabasePath);
            var godotDict = Json.ParseString(jsonText).Obj as Godot.Collections.Dictionary;
            userDatabase = new Dictionary<string, string>();
            if (godotDict != null)
            {
                foreach (var key in godotDict.Keys)
                {
                    userDatabase[key.ToString()] = godotDict[key].ToString();
                }
            }
        }
    }

    private void SaveUserDatabase()
    {
        var godotDict = new Godot.Collections.Dictionary<string, string>();
        foreach (var kvp in userDatabase)
        {
            godotDict[kvp.Key] = kvp.Value;
        }
        File.WriteAllText(userDatabasePath, Json.Stringify(godotDict));
    }

    private void LoadPlayerPositions()
    {
        if (File.Exists(playerPositionsPath))
        {
            string jsonText = File.ReadAllText(playerPositionsPath);
            var parsedData = Json.ParseString(jsonText).Obj as Godot.Collections.Dictionary;
            playerPositions = new Dictionary<string, Vector2>();
            if (parsedData != null)
            {
                foreach (var key in parsedData.Keys)
                {
                    playerPositions[key.ToString()] = (Vector2)parsedData[key];
                }
            }
        }
    }

    private void SavePlayerPositions()
    {
        var godotDict = new Godot.Collections.Dictionary<string, Variant>();
        foreach (var kvp in playerPositions)
        {
            godotDict[kvp.Key] = kvp.Value;
        }
        File.WriteAllText(playerPositionsPath, Json.Stringify(godotDict));
    }

    [Rpc]
    private void Authenticate(int id, string jsonString)
    {
        var loginDataVariant = Json.ParseString(jsonString).Obj as Godot.Collections.Dictionary;
        var loginData = new Dictionary<string, string>();
        if (loginDataVariant != null)
        {
            foreach (var key in loginDataVariant.Keys)
            {
                loginData[key.ToString()] = loginDataVariant[key].ToString();
            }
        }

        if (!loginData.ContainsKey("username") || !loginData.ContainsKey("password"))
        {
            SendJsonResponse(id, "error", "Invalid request format.");
            return;
        }

        string username = loginData["username"];
        string password = loginData["password"];

        if (loggedInUsers.Contains(username))
        {
            SendJsonResponse(id, "error", "User already logged in.");
            return;
        }

        if (userDatabase.ContainsKey(username) && VerifyPassword(password, userDatabase[username]))
        {
            Vector2 spawnPos = playerPositions.ContainsKey(username) ? playerPositions[username] : new Vector2(GD.Randf() * 400, GD.Randf() * 400);
            playerPositions[username] = spawnPos;
            loggedInUsers.Add(username);

            SendJsonResponse(id, "success", "Login successful.");
            RpcId(id, "InitializePlayer", id, spawnPos);
        }
        else
        {
            SendJsonResponse(id, "error", "Invalid username or password.");
        }
    }

    [Rpc]
    private void SignUp(int id, string jsonString)
    {
        var signupData = Json.ParseString(jsonString).Obj as Godot.Collections.Dictionary;
        var signupDataDict = new Dictionary<string, string>();
        foreach (var key in signupData.Keys)
        {
            signupDataDict[key.ToString()] = signupData[key].ToString();
        }

        if (!signupData.ContainsKey("username") || !signupData.ContainsKey("password"))
        {
            SendJsonResponse(id, "error", "Invalid request format.");
            return;
        }

        string username = (string)signupData["username"];
        string password = (string)signupData["password"];

        if (userDatabase.ContainsKey(username))
        {
            SendJsonResponse(id, "error", "Username already exists.");
            return;
        }

        userDatabase[username] = HashPassword(password);
        SaveUserDatabase();

        SendJsonResponse(id, "success", "Account created successfully.");
    }

    private void SendJsonResponse(int id, string status, string message)
    {
        var responseDict = new Godot.Collections.Dictionary<string, string> { { "status", status }, { "message", message } };
        RpcId(id, "ServerResponse", Json.Stringify(responseDict));
    }

    private string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

    private bool VerifyPassword(string enteredPassword, string storedHash)
    {
        return HashPassword(enteredPassword) == storedHash;
    }
}
