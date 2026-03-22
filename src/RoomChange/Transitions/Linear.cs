using UnityEngine;

namespace RoomChange.Transitions;

public class Linear
{
    const float epsilon = 0.0001f;

    //Relative path in A to B
    public static float GetBlend(float now, float pretime, float time)
    {
        if (Mathf.Abs(time - pretime) < epsilon)
        {
            PDEBUG.Log("Division by zero in RateChanges.Linear");
            return 0f;
        }

        float delta = (now - pretime) / (time - pretime);
        //PDEBUG.Log($"Actual Time: {now}, nextPaletteTime: {time}, prevPaletteTime: {pretime}, paletteBlend: %{delta * 100}");
        return delta;
    }
}
