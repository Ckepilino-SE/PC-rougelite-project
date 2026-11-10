using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gameplay.UI
{
    /// <summary>
    /// TEMPORARY debugging aid. On every left click, logs every UI element under the mouse (topmost first)
    /// and whether each blocks clicks. Put it on the EventSystem, reproduce the problem, then remove it.
    /// </summary>
    public sealed class UIClickDebugger : MonoBehaviour
    {
        private readonly List<RaycastResult> results = new List<RaycastResult>();
        private readonly StringBuilder log = new StringBuilder();

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogWarning("[UIClickDebugger] No active EventSystem in the scene.");
                return;
            }

            var pointer = new PointerEventData(eventSystem) { position = mouse.position.ReadValue() };
            results.Clear();
            eventSystem.RaycastAll(pointer, results);

            log.Clear();
            log.Append("[UIClickDebugger] Click at ").Append(pointer.position).Append(" hit ").Append(results.Count).Append(" UI element(s), topmost first:");

            for (int i = 0; i < results.Count; i++)
            {
                GameObject go = results[i].gameObject;
                log.Append("\n  ").Append(i + 1).Append(". ").Append(Path(go.transform));

                Selectable selectable = go.GetComponentInParent<Selectable>();
                if (selectable != null)
                {
                    log.Append("  -> button '").Append(selectable.name).Append("' interactable=").Append(selectable.IsInteractable());
                }

                CanvasGroup group = go.GetComponentInParent<CanvasGroup>();
                if (group != null)
                {
                    log.Append("  [CanvasGroup '").Append(group.name).Append("' alpha=").Append(group.alpha.ToString("0.00"))
                        .Append(" interactable=").Append(group.interactable).Append(" blocksRaycasts=").Append(group.blocksRaycasts).Append(']');
                }
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            log.Append("\n  Selected: ").Append(selected != null ? selected.name : "none");
            log.Append("   timeScale=").Append(Time.timeScale);
            Debug.Log(log.ToString(), this);
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
