using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    public class WalkEnemyManager : MonoBehaviour
    {
        [Tooltip("Define the direction of the movement\nor rotate the object toward the movement direction")]
        [ReadOnly] public Vector3 Target; // Define the direction of the object when spawn or use rotation to calculate the direction...

        public bool FollowOnStart;
        public bool StartMove;
        public float MoveSpeed;
        public string FollowedTag;
        public Transform RoatateModel;

        [BigHeader("Animation Settings")]
        public Animator AnimationController;
        public string RunForward_bool, Defeated_bool;

        Transform _charFollow;
        Collider _charFollowCol;
        bool _followTag;
        float _nextSearch; // with no live target, the tag search runs a few times a second instead of every frame
        const float SearchInterval = 0.25f;
        void Start()
        {
            if (!RoatateModel) RoatateModel = transform;
            if (Target == Vector3.zero) Target = RoatateModel.TransformDirection(Vector3.forward);
            RoatateModel.LookAt(Target.normalized + transform.position);
            if (FollowOnStart) StartFollow();
        }

        // Update is called once per frame
        void Update()
        {
            if (StartMove)
            {
                if (!_followTag)
                {
                    AnimationController.SetBool(RunForward_bool, true);
                    transform.position += MoveSpeed * Time.deltaTime * Target;
                }
                else
                {
                    if (_charFollow && _charFollowCol && _charFollowCol.enabled)
                    {
                        Vector3 target = -transform.position + _charFollow.position; target.y = 0;
                        RoatateModel.rotation = Quaternion.LookRotation(target);
                        transform.position += Mathf.Abs(MoveSpeed) * Time.deltaTime * RoatateModel.TransformDirection(Vector3.forward);
                    }
                    else if (!ReferenceEquals(_charFollow, null) || Time.time >= _nextSearch) FindTarget(); // target just lost: look again at once
                }
            }
        }

        public void StartFollow()
        {
            _followTag = true;
            FindTarget();
        }

        void FindTarget() // first object with the followed tag whose collider is still enabled
        {
            _nextSearch = Time.time + SearchInterval;
            _charFollow = null;
            _charFollowCol = null;
            foreach (var go in GameObject.FindGameObjectsWithTag(FollowedTag))
            {
                if (!go.TryGetComponent(out Collider col) || !col.enabled) continue;
                _charFollow = go.transform;
                _charFollowCol = col;
                break;
            }
            AnimationController.SetBool(RunForward_bool, _charFollow != null);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == 10)
            {
                StartFollow();
            }
        }

        public void DestroyChild() // remove this child
        {
            StartMove = false;
            AnimationController.SetBool(Defeated_bool, true);
            GetComponent<MOST_Damage>().InstantKill();
        }

        public void ResetData()
        {
            AnimationController.SetBool(Defeated_bool, false);
            GetComponent<MOST_Damage>().Revive();
            StartMove = true;
            RoatateModel.LookAt(Target.normalized + transform.position);
            if (FollowOnStart) StartFollow();
        }
    }
}