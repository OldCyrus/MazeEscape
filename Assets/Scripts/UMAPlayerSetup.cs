using UnityEngine;
using Unity.Netcode;
using UMA;
using UMA.CharacterSystem;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;
using System;
using System.Collections.Generic;

namespace MazeEscape
{
    [System.Serializable]
    public class AttachNodeOverride
    {
        public string nodeName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
    }

    public class UMAPlayerSetup : NetworkBehaviour, IPlayerAddon
    {
        private const string RecipePrefsKey = "UMA_PlayerRecipe";

        [SerializeField] private DynamicCharacterAvatar avatar;
        [SerializeField] private AttachNodeOverride[] nodeOverrides = new AttachNodeOverride[0];

        private bool m_Reported;
        private Animator m_RobotAnimator;
        private Animator m_UMAAnimator;
        private bool m_SyncedOnce;
        private bool m_ReparentDone;

        public void Initialize(CorePlayerManager playerManager)
        {
            if (avatar == null)
                avatar = GetComponentInChildren<DynamicCharacterAvatar>(includeInactive: true);
        }

        public void OnPlayerSpawn()
        {
            if (!IsOwner) return;
            if (avatar == null) { ReportReady(); return; }
            avatar.CharacterCreated.AddListener(OnCharacterCreated);
            avatar.CharacterUpdated.AddListener(OnCharacterUpdated);
        }

        public void OnPlayerDespawn()
        {
            if (avatar != null)
            {
                avatar.CharacterCreated.RemoveListener(OnCharacterCreated);
                avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
            }
            m_RobotAnimator = null;
            m_UMAAnimator = null;
            m_SyncedOnce = false;
            m_ReparentDone = false;
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState) { }

        private void LateUpdate()
        {
            if (m_RobotAnimator == null || m_UMAAnimator == null) return;

            // Mirror robot animator state to UMA every frame.
            foreach (var param in m_RobotAnimator.parameters)
            {
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        m_UMAAnimator.SetFloat(param.nameHash, m_RobotAnimator.GetFloat(param.nameHash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        m_UMAAnimator.SetBool(param.nameHash, m_RobotAnimator.GetBool(param.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        m_UMAAnimator.SetInteger(param.nameHash, m_RobotAnimator.GetInteger(param.nameHash));
                        break;
                }
            }

            for (int i = 0; i < m_RobotAnimator.layerCount; i++)
                m_UMAAnimator.SetLayerWeight(i, m_RobotAnimator.GetLayerWeight(i));

            for (int i = 0; i < m_RobotAnimator.layerCount; i++)
            {
                var src = m_RobotAnimator.GetCurrentAnimatorStateInfo(i);
                var dst = m_UMAAnimator.GetCurrentAnimatorStateInfo(i);
                if (src.fullPathHash != dst.fullPathHash)
                    m_UMAAnimator.Play(src.fullPathHash, i, src.normalizedTime);
            }

            // Reparent weapon attachment nodes to UMA bones on the frame AFTER the first
            // sync completes. At that point both animators are in the same pose so the
            // bone local axes match and worldPositionStays:false gives correct positions.
            if (!m_ReparentDone)
            {
                if (m_SyncedOnce)
                {
                    ReparentAttachableNodes();
                    m_ReparentDone = true;
                }
                m_SyncedOnce = true;
            }
        }

        private void OnCharacterCreated(UMAData umaData)
        {
            var geometry = transform.root.Find("Armature_Shooter/Geometry");
            if (geometry != null) geometry.gameObject.SetActive(false);

            ActivateSync(umaData);

            string recipe = PlayerPrefs.GetString(RecipePrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(recipe))
            {
                avatar.LoadFromRecipeString(recipe,
                    DynamicCharacterAvatar.LoadOptions.loadRace |
                    DynamicCharacterAvatar.LoadOptions.loadDNA |
                    DynamicCharacterAvatar.LoadOptions.loadWardrobe |
                    DynamicCharacterAvatar.LoadOptions.loadBodyColors |
                    DynamicCharacterAvatar.LoadOptions.loadWardrobeColors);
            }

            ReportReady();
        }

        private void OnCharacterUpdated(UMAData umaData)
        {
            avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
            ActivateSync(umaData);
        }

        private void ActivateSync(UMAData umaData)
        {
            if (umaData.animator == null) return;
            umaData.animator.fireEvents = false;
            m_RobotAnimator = transform.root.Find("Armature_Shooter")?.GetComponent<Animator>();
            m_UMAAnimator = umaData.animator;
        }

        private void ReparentAttachableNodes()
        {
            if (m_RobotAnimator == null || m_UMAAnimator == null) return;

            var boneMap = new Dictionary<Transform, Transform>();
            foreach (HumanBodyBones bone in (HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                var rb = m_RobotAnimator.GetBoneTransform(bone);
                var ub = m_UMAAnimator.GetBoneTransform(bone);
                if (rb != null && ub != null) boneMap[rb] = ub;
            }

            foreach (var node in transform.root.GetComponentsInChildren<AttachableNode>(includeInactive: true))
            {
                Transform robotBone = node.transform.parent;
                if (robotBone == null) continue;
                if (!boneMap.TryGetValue(robotBone, out Transform umaBone)) continue;

                // Compute desired world transform using robot bone orientation (correct for this rig's
                // local axes) but anchor position to the UMA bone so the weapon sits on the visible character.
                Vector3 desiredPos = umaBone.position + robotBone.rotation * node.transform.localPosition;
                Quaternion desiredRot = robotBone.rotation * node.transform.localRotation;

                node.transform.SetParent(umaBone, worldPositionStays: false);
                node.transform.position = desiredPos;
                node.transform.rotation = desiredRot;

                foreach (var ov in nodeOverrides)
                {
                    if (ov.nodeName == node.gameObject.name)
                    {
                        node.transform.localPosition = ov.localPosition;
                        node.transform.localEulerAngles = ov.localEulerAngles;
                        break;
                    }
                }
            }
        }

        private void ReportReady()
        {
            if (m_Reported) return;
            m_Reported = true;
            if (MatchManager.Instance != null)
                MatchManager.Instance.ReportReadyServerRpc();
            else
                Debug.LogWarning("[UMAPlayerSetup] MatchManager not found when reporting ready.", this);
        }
    }
}
