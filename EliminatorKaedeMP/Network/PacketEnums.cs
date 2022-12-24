namespace EliminatorKaedeMP
{
    // Server -> Client
    public enum S2CPacketID
    {
        GameJoinInfo,
		SceneChange,
		PlayerJoin,
        PlayerLeave,
        PlayerMove,
        PlayerJump,
        PlayerCtrlKey,
        PlayerKnifeUse,
		PlayerChangeChar
    }

    // Client -> Server
    public enum C2SPacketID
    {
        PlayerMove,
        PlayerJump,
        PlayerCtrlKey,
        PlayerKnifeUse,
        PlayerChangeChar
    }
}
