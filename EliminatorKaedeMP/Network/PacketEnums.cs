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
        PlayerKnifeUse,
		SceneChange
    }

    // Client -> Server
    public enum C2SPacketID
    {
        PlayerMove,
        PlayerJump,
        PlayerCtrlKey,
        PlayerKnifeUse
    }
}
