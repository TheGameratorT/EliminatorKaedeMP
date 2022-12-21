namespace EliminatorKaedeMP
{
    // Server -> Client
    public enum S2CPacketID
    {
        GameJoinInfo,
        PlayerJoin,
        PlayerLeave,
        PlayerMove,
        SceneChange
    }

    // Client -> Server
    public enum C2SPacketID
    {
        PlayerMove
    }
}
