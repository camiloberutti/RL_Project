using UnityEngine;

public class RewardTrigger : MonoBehaviour
{
    public ShooterAgent myAgent;

    private int cansFallen = 0;

    void OnTriggerEnter(Collider other)
    {
        // --- A pig/can fell into the trigger zone ---
        if (other.CompareTag("Target"))
        {
            myAgent.AddReward(0.2f);
            cansFallen++;

            // Disable collider to prevent double-counting on bounces
            other.enabled = false;

            // All 6 pigs cleared — bonus reward and early episode end
            if (cansFallen >= 6)
            {
                myAgent.AddReward(1.0f);
                myAgent.EndEpisode();
            }
        }

        // --- The bird itself hit the floor (clean miss) ---
        if (other.CompareTag("Bird"))
        {
            myAgent.AddReward(-0.3f);  // Penalize missing entirely
            myAgent.EndEpisode();      // Don't waste 3 seconds on a confirmed miss
        }
    }

    // Called by ShooterAgent on each episode reset
    public void ResetTrigger()
    {
        cansFallen = 0;
    }
}