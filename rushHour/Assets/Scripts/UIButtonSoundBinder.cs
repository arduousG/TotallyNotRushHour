using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class UIButtonSoundBinder : MonoBehaviour
{
    [SerializeField] private bool includeInactive = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogBindingEvents = false;

    private readonly List<Button> boundButtons = new List<Button>();
    private readonly Dictionary<Button, UnityAction> buttonClickActions = new Dictionary<Button, UnityAction>();

    private void Start()
    {
        BindButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void BindButtons()
    {
        UnbindButtons();

        Button[] buttons = GetComponentsInChildren<Button>(includeInactive);
        foreach (Button button in buttons)
        {
            UnityAction clickAction = delegate
            {
                PlayClickSoundForButton(button);
            };

            button.onClick.AddListener(clickAction);
            buttonClickActions[button] = clickAction;
            boundButtons.Add(button);

            if (debugLogBindingEvents)
            {
                Debug.Log("UIButtonSoundBinder bound to button: " + button.name);
            }
        }

        if (debugLogBindingEvents)
        {
            Debug.Log("UIButtonSoundBinder total buttons bound: " + boundButtons.Count);
        }
    }

    private void UnbindButtons()
    {
        foreach (Button button in boundButtons)
        {
            if (button != null)
            {
                if (buttonClickActions.TryGetValue(button, out UnityAction clickAction))
                {
                    button.onClick.RemoveListener(clickAction);
                }
            }
        }

        boundButtons.Clear();
        buttonClickActions.Clear();
    }

    private void PlayClickSoundForButton(Button button)
    {
        if (debugLogBindingEvents && button != null)
        {
            Debug.Log("UIButtonSoundBinder click fired from: " + button.name);
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayUIClick();
        }
    }
}
