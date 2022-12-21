namespace EliminatorKaedeMP
{
    // Server -> Client
    public enum S2CPacketID
    {
        GameJoinInfo,
        PlayerJoin,
        PlayerLeave,
        PlayerMove,
        PlayerJump,
        PlayerCtrlKey,
        SceneChange
    }

    // Client -> Server
    public enum C2SPacketID
    {
        PlayerMove,
        PlayerJump,
        PlayerCtrlKey
    }
}
