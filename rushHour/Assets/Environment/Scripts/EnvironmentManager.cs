using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    public GameObject beginnerEnvironment;
    public GameObject intermediateEnvironment;
    public GameObject advancedEnvironment;
    public GameObject expertEnvironment;

    public Transform environmentAnchor;

    private GameObject currentEnvironment;

    public void LoadEnvironment(PuzzleController.Diff difficulty)
    {
        if(currentEnvironment != null)
        {
            Destroy(currentEnvironment);
        }

        GameObject environmentToSpawn=null;

        switch(difficulty)
        {
            case PuzzleController.Diff.Beginner:
                environmentToSpawn=beginnerEnvironment;
                break;

            case PuzzleController.Diff.Intermediate:
                environmentToSpawn=intermediateEnvironment;
                break;

            case PuzzleController.Diff.Advanced:
                environmentToSpawn=advancedEnvironment;
                break;

            case PuzzleController.Diff.Expert:
                environmentToSpawn=expertEnvironment;
                break;
        }

        if(environmentToSpawn!=null)
        {
            currentEnvironment=
                Instantiate(
                    environmentToSpawn,
                    environmentAnchor.position,
                    Quaternion.identity
                );
        }
    }

    public void HideEnvironment()
    {
        if(currentEnvironment!=null)
        {
            Destroy(currentEnvironment);
        }
    }
}