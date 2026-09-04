using UnityEngine;
using UnityEngine.UI;

namespace PirateSlop
{
    public class InteractionHud : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Text label;
        [SerializeField] Text status;

        public void Bind(PlayerInteractor source, Text promptLabel, Text statusLabel)
        {
            interactor = source;
            label = promptLabel;
            status = statusLabel;
        }

        void LateUpdate()
        {
            if (label != null)
            {
                var focus = interactor != null ? interactor.Focus : null;
                bool show = focus != null && focus.IsAvailable;
                label.enabled = show;
                if (show) label.text = focus.Prompt;
            }

            if (status != null)
            {
                var ship = FindAnyObjectByType<ShipMotor>();
                if (ship == null)
                {
                    status.enabled = false;
                    return;
                }

                status.enabled = true;
                status.text = string.Format("Sail {0:0}%   Speed {1:0.0} m/s   Helm {2:0.00}",
                    ship.Throttle * 100f, ship.Speed, ship.Rudder);
            }
        }
    }
}
