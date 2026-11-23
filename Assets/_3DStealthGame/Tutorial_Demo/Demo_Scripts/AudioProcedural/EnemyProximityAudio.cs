using UnityEngine;

public class EnemyProximityAudio : MonoBehaviour
{
    [System.Serializable]
    public class EnemyGroup
    {
        public string tag;
        public ContinuousSfx sound;
        public float maxDistance = 5f;
    }

    public Transform player;
    public EnemyGroup[] enemyGroups;

    private void Update()
    {
        if (player == null) return;

        foreach (EnemyGroup group in enemyGroups)
        {
            GameObject[] enemies =
                GameObject.FindGameObjectsWithTag(group.tag);

            float closestDistance = Mathf.Infinity;

            foreach (GameObject enemy in enemies)
            {
                float distance = Vector3.Distance(
                    player.position,
                    enemy.transform.position
                );

                if (distance < closestDistance)
                    closestDistance = distance;
            }

            if (closestDistance <= group.maxDistance)
            {
                float volume = 1f - Mathf.Clamp01(
                    closestDistance / group.maxDistance
                );

                ContinuousProceduralSfx.PlayContinuous(
                    group.sound, volume
                );

                ContinuousProceduralSfx.SetVolume(
                    group.sound, volume
                );
            }
            else
            {
                ContinuousProceduralSfx.StopContinuous(
                    group.sound
                );
            }
        }
    }
}