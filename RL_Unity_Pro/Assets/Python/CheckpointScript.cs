using UnityEngine;

/// <summary>
/// Har checkpoint pe yeh script lagao
/// Inspector mein index set karo (0, 1, 2, 3...)
/// </summary>
public class CheckpointScript : MonoBehaviour
{
    public int index = 0;  // Checkpoint number

    private void OnDrawGizmos()
    {
        // Editor mein checkpoint dikhane ke liye
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}
