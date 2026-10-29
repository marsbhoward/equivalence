using UnityEngine;
using UnityEngine.UI;
using Convergence.Chain.Service;

namespace Convergence.UI
{
    /// <summary>
    /// One line across the top of the screen: "reconnecting" while a request waits out an outage
    /// (<see cref="ServiceClient.Reconnecting"/>), else what the service has paused
    /// (<see cref="ServiceStatus.Banner"/>). Nothing at all the rest of the time. Its own canvas,
    /// above every screen, so it reads through a menu too; it catches no input.
    /// </summary>
    public class ServiceBanner : MonoBehaviour
    {
        Text _text;
        GameObject _panel;
        string _shown;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("ServiceBanner", 40);
            canvas.transform.SetParent(transform, false);
            foreach (var r in canvas.GetComponents<GraphicRaycaster>()) r.enabled = false;
            var bar = UiKit.Panel((RectTransform)canvas.transform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -34), new Vector2(0, 0), new Color(0.05f, 0.05f, 0.08f, 0.86f));
            _panel = bar.gameObject;
            _text = UiKit.Label(bar, "", 18, new Color(0.95f, 0.85f, 0.55f), TextAnchor.MiddleCenter);
            foreach (var g in _panel.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            _panel.SetActive(false);
        }

        void Update()
        {
            if (_text == null) return;
            string line = ServiceClient.Reconnecting > 0 ? "RECONNECTING TO THE SERVER..." : ServiceStatus.Banner;
            if (line == _shown) return;
            _shown = line;
            _text.text = line;
            _panel.SetActive(!string.IsNullOrEmpty(line));
        }
    }
}
