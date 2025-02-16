using Godot;
using System;



public partial class Server : Node
{
	int port = 1234;
	int max_players = 100;
	
	// Start the server
	public override void _Ready()
	{
		StartServer();
	}

	
	public void StartServer()
	{
		var network = new ENetMultiplayerPeer();
		network.CreateServer(port, max_players);
		Multiplayer.MultiplayerPeer = network;
		Multiplayer.PeerConnected += OnPlayerConnected;
        Multiplayer.PeerDisconnected += OnPlayerDisconnected;
		GD.Print("Server Started");
	}

	public void OnPlayerConnected(long player_id)
	{
		GD.Print((player_id) + "Connected");
	
	}
	public void OnPlayerDisconnected(long player_id)
	{
		GD.Print((player_id) + "Disconnected");
	}
}
