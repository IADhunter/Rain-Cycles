sampler2D _PreLevelColorGrab;

inline bool CreatureMask ( int red, float2 scrPos ) {
    return (((red.x - 1) % 30) > 5) * tex2D(_PreLevelColorGrab, scrPos) == 0 ;
}
