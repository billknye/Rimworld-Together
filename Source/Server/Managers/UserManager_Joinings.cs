using RimworldTogether.GameServer.Core;
using RimworldTogether.GameServer.Network;
using RimworldTogether.Shared.JSON;
using RimworldTogether.Shared.Misc;
using RimworldTogether.Shared.Network;

namespace RimworldTogether.GameServer.Managers;

public class UserManager_Joinings
{
    private readonly Network.Network network;

    public enum CheckMode { Login, Register }

    public enum LoginResponse
    {
        InvalidLogin,
        BannedLogin,
        RegisterSuccess,
        RegisterInUse,
        RegisterError,
        ExtraLogin,
        WrongMods,
        ServerFull,
        Whitelist
    }

    public UserManager_Joinings(Network.Network network)
    {
        this.network = network;
    }

    public bool CheckLoginDetails(Client client, CheckMode mode)
    {
        bool isInvalid = false;
        if (string.IsNullOrWhiteSpace(client.username)) isInvalid = true;
        if (client.username.Any(Char.IsWhiteSpace)) isInvalid = true;
        if (string.IsNullOrWhiteSpace(client.password)) isInvalid = true;
        if (client.username.Length > 32) isInvalid = true;

        if (!isInvalid) return true;
        else
        {
            if (mode == CheckMode.Login) SendLoginResponse(network, client, LoginResponse.InvalidLogin);
            else if (mode == CheckMode.Register) SendLoginResponse(network, client, LoginResponse.RegisterError);
            return false;
        }
    }

    public static void SendLoginResponse(Network.Network network, Client client, LoginResponse response, object extraDetails = null)
    {
        LoginDetailsJSON loginDetailsJSON = new LoginDetailsJSON();
        loginDetailsJSON.tryResponse = ((int)response).ToString();

        if (response == LoginResponse.WrongMods) loginDetailsJSON.conflictingMods = (List<string>)extraDetails;

        string[] contents = new string[] { Serializer.SerializeToString(loginDetailsJSON) };
        Packet packet = new Packet("LoginResponsePacket", contents);
        network.SendData(client, packet);

        client.disconnectFlag = true;
    }

    public bool CheckWhitelist(Client client)
    {
        if (!Program.whitelist.UseWhitelist) return true;
        else
        {
            foreach (string str in Program.whitelist.WhitelistedUsers)
            {
                if (str == client.username) return true;
            }
        }

        SendLoginResponse(network, client, LoginResponse.Whitelist);

        return false;
    }
}
