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
            Plugin.RSPlugin.log.LogWarning("[Linear] Division by zero in GetBlend.");
            return 0f;
        }

        float delta = (now - pretime) / (time - pretime);
        return delta;
    }
}