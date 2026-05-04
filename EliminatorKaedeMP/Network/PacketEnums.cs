namespace EliminatorKaedeMP
{
    // Server -> Client
    public enum S2CPacketID
    {
        GameJoinInfo,    // 0 - full game state on join
        SceneChange,     // 1 - server-driven scene load
        PlayerJoin,      // 2 - a new player connected
        PlayerLeave,     // 3 - a player disconnected
        PlayerState,     // 4 - 30Hz movement + game state (replaces PlayerMove + FlyState)
        PlayerEvent,     // 5 - discrete events (replaces PlayerJump + PlayerCtrlKey + PlayerKnifeUse)
        PlayerHealth,    // 6 - 5Hz health state broadcast
        PlayerChangeChar,// 7
        PlayerClothInfo, // 8
        ToiletState      // 9 - toilet event synchronization
    }

    // Client -> Server
    public enum C2SPacketID
    {
        PlayerState,     // 0 - 30Hz movement + game state
        PlayerEvent,     // 1 - discrete events
        PlayerHealth,    // 2 - 5Hz health state
        PlayerChangeChar,// 3
        PlayerClothInfo, // 4
        UdpHandshake,    // 5 - registers client UDP endpoint with server (payload: uint32 playerID)
        ToiletState,     // 6 - toilet event synchronization
    }

    // Discrete gameplay events (sent inside a PlayerEvent packet)
    public enum PlayerEventID
    {
        Jump = 0,             // data0 = jumpType (1=roll_fwd, 2=roll_back, 3=aim_jump, 4=running_jump, 5=standing_jump)
        CtrlKey = 1,          // data0 = CtrlKey enum, data1 = 1 (down) or 0 (up)
        KnifeUse = 2,         // data0 = step (0,1,2)
        DamageExploFront = 3, // data0 = 1 (front) or 0 (back)
        Vomit = 4,            // data0 = unused
        GunFire = 5,          // data0 = unused (visual only)
        Reload = 6,           // data0 = unused
        WeaponSwitch = 7,     // data0 = weapon slot (0=main, 1=sub)
        Grenade = 8,          // data0 = grenade state (GranadeState enum)
        ToiletStateChange = 9, // data0 = ToiletEventManager.Type, data1 = ToiletEventManager.State
    }
}
