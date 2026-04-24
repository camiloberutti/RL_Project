using UnityEngine;

public class RewardTrigger : MonoBehaviour
{
    public ShooterAgent myAgent;
    private int cansFallen = 0;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            // 1. Give the AI its dopamine hit
            myAgent.AddReward(0.2f);
            cansFallen++;

            // 2. Disable the can's collider so it doesn't double-count if it bounces!
            other.enabled = false;

            // 3. If all 6 cans fall...
            if (cansFallen >= 6)
            {
                myAgent.AddReward(1.0f);
                myAgent.EndEpisode();
            }
        }
    }

    // Instead of FixedUpdate, let's make a public function the Agent can call
    public void ResetTrigger()
    {
        cansFallen = 0;
    }
}