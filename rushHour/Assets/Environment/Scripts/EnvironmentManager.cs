using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    public GameObject beginnerEnvironment;
    public GameObject intermediateEnvironment;
    public GameObject advancedEnvironment;
    public GameObject expertEnvironment;

    private GameObject currentEnvironment;

    public void LoadEnvironment(PuzzleController.Diff diff)
    {
        Debug.Log("Loading environment: " + diff);

        HideEnvironment();

        GameObject envToLoad = null;

        switch(diff)
        {
            case PuzzleController.Diff.Beginner:
                envToLoad = beginnerEnvironment;
                break;

            case PuzzleController.Diff.Intermediate:
                envToLoad = intermediateEnvironment;
                break;

            case PuzzleController.Diff.Advanced:
                envToLoad = advancedEnvironment;
                break;

            case PuzzleController.Diff.Expert:
                envToLoad = expertEnvironment;
                break;
        }

        if(envToLoad != null)
        {
            currentEnvironment =
                Instantiate(envToLoad);

            Debug.Log("Environment spawned");
        }
        else
        {
            Debug.Log("Environment prefab missing");
        }
    }

    public void HideEnvironment()
    {
        if(currentEnvironment != null)
        {
            Destroy(currentEnvironment);
        }
    }
}