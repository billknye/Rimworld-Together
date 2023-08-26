namespace RimworldTogether.GameServer.Misc;

public static class Threader
{
    public enum ServerMode
    {
        Start,
        Heartbeat,
        Sites,
        Console
    }

    public enum ClientMode
    {
        Start
    }

}