using UnityEngine;

namespace MazeEscape
{
    // Keeps the preview avatar rooted at ground level in the character creation scene.
    // UMA can add a CharacterController at runtime which lets gravity act on the avatar;
    // this script counteracts that by locking Y to the spawn position every LateUpdate.
    public class CharacterCreationAvatarAnchor : MonoBehaviour
    {
        private float m_GroundY;

        private void Awake()
        {
            m_GroundY = transform.position.y;
        }

        private void LateUpdate()
        {
            var pos = transform.position;
            if (pos.y != m_GroundY)
            {
                pos.y = m_GroundY;
                transform.position = pos;
            }
        }
    }
}
