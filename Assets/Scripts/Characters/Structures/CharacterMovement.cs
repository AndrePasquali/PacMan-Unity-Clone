using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Threading.Tasks;
using Aquiris.Core.Constants;
using Aquiris.PacMan.Characters.State;
using Aquiris.PacMan.PathFind;
using PathFind;
using UnityEngine;

namespace Aquiris.PacMan.Characters
{
    public class CharacterMovement: MonoBehaviour
    {
        public float MovementSpeed = 1.0F;

        #region Components
        
        public CharacterController CharacterController => _characterController ?? (_characterController = GetComponent<CharacterController>());

        protected CharacterController _characterController;

        public PacManCharacter Character => _character ?? (_character = GetComponent<PacManCharacter>());

        private PacManCharacter _character;      
                
        public Animator Animator => _animator ?? (_animator = GetComponent<Animator>());

        private Animator _animator;

        #endregion
        
        [HideInInspector]
        public List<Vector2> RaysDirections = new List<Vector2>();

        [SerializeField]
        private float _timeToStartMove = 2.5F;

        private Vector3 _startPosition;

        public float DistanceView = 3.0F;

        public float OriginRayDistance = 1.0F;

        protected int _currentWayPoint = 0;

        protected float _lastUpdateTime;

        public LayerMask ObstaclesLayer;

        public float DistanceDelta = 5.0F;

        public List<CharacterDirection> CharacterAllowedDirections = new List<CharacterDirection>();
        
        public List<Vector2> VectorDirectionList = new List<Vector2>{Vector2.left, Vector2.right, Vector2.down, Vector2.up};

        private Vector2 _previousPosition;

        private Vector2 _currentPosition;
        
        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            PopulateDirections();           
            StoreInitialPosition();
            AuthorizingWalkAfterTime();
            FixPosition();
        }
        
        public void EveryFrame()
        {
            SetDirectionByInput();
            ProcessMove();
            //ProcessVision();
            HandleStopMovement();
        }

        private void FixedUpdate()
        {
            EveryFrame();
        }

        private void FixPosition()
        {
            transform.position = GameConstants.INITIAL_PACMAN_POSITION;
        }

        #region Movement
        
        private void ProcessMove()
        {
            if(Character.State.ConditionState == CharacterCondition.Dead || Character.State.ConditionState == CharacterCondition.Freeze) return;
            
            var direction = (Vector3) CharacterDirectionHelper.GetVectorByDirectionState(Character.State.DirectionState);
            
            if(!CharacterAllowedDirections.Contains(Character.State.DirectionState)) return;

            var targetPosition = transform.position + direction * Time.fixedDeltaTime * MovementSpeed;

            var position = Vector2.MoveTowards(transform.position,
                targetPosition,
                DistanceDelta);
            
            CharacterController.Rigibody.MovePosition(position);        
        }
        
        private void HandleStopMovement()
        {
            var currentDirection = CharacterDirectionHelper.GetVectorByDirectionState(Character.State.DirectionState);

            var isFacingWall = IsCollidingWalls(currentDirection);

            Animator.speed = isFacingWall ? 0 : 1F;
        }
   
        public void ProcessMovement()
        {            
            var controller = CharacterController;         

            var desiredDirection = GetTargetDirection();
            
            if(!IsValidDirection()) return;

            var desiredPosition = Vector2.MoveTowards(transform.position, (Vector2)transform.position + desiredDirection,
                controller.MovementSpeed);
            
            controller.Rigibody.MovePosition(desiredPosition);
            
            //transform.Translate(Vector2.MoveTowards(controller.CurrentPosition, controller.NewPosition, controller.MovementPointSpeed), Space.Self);            
        }

        private void ProcessVision()
        {
            var originDistance = 1.0F;
            var distanceView = 3.0F;
            var distanceFromWall = 0F;

            //We cast some rays for detect correct path
            for (int i = 0; i < VectorDirectionList.Count; i++)
            {
                var currentDirection = VectorDirectionList[i];

                var rayOrigin = (Vector2) transform.position + currentDirection * originDistance;

                //  rayOrigin.x = collider.offset.x + collider.bounds.min.x;

                var hit = Physics2D.Raycast(rayOrigin, currentDirection * distanceView);

                var characterDirection = CharacterDirectionHelper.GetDirectionByVector(currentDirection);

                //if(characterDirection == CharacterDirection.Left || characterDirection == CharacterDirection.Down) continue;
                if (hit.transform != null)
                    if (hit.collider.CompareTag(GameConstants.WALL_TAG))
                        CharacterAllowedDirections.RemoveAll(e => e == characterDirection);
                    else if (!CharacterAllowedDirections.Exists(e => e == characterDirection))
                        CharacterAllowedDirections.Add(characterDirection);

                Debug.DrawRay(rayOrigin, currentDirection * distanceView);

            }
        }


        #endregion

        #region Directions

        public void SetDirectionByInput()
        {
            var currentInputSpeed = Character.LinkedInputController.Movement;

            if (currentInputSpeed.x > 0)
            {
                if(IsValidDirection(CharacterDirection.Right))
                    ChangeDirection(CharacterDirection.Right);
            }
            else if (currentInputSpeed.x < 0)
            {
                if(IsValidDirection(CharacterDirection.Left))
                    ChangeDirection(CharacterDirection.Left);
            }
            else if (currentInputSpeed.y > 0)
            {
                if(IsValidDirection(CharacterDirection.Up))
                    ChangeDirection(CharacterDirection.Up);
            }
            else if (currentInputSpeed.y < 0)
            {
                if(IsValidDirection(CharacterDirection.Down))
                    ChangeDirection(CharacterDirection.Down);
            }
            else if(currentInputSpeed.x == 0 || currentInputSpeed.y == 0) ChangeDirection(Character.State.DirectionState);                    
        }

        public void ChangeDirection(CharacterDirection newDirection)
        {
            Character.State.ChangeDirectionState(newDirection);
        }
        
        public void UpdateAllowedDirections(List<CharacterDirection> characterDirections)
        {
            CharacterAllowedDirections = characterDirections;
        }

        protected bool IsValidDirection()
        {
            return CharacterAllowedDirections.Contains(Character.State.DirectionState);
        }

        protected bool IsValidDirection(CharacterDirection direction)
        {
            return CharacterAllowedDirections.Contains(direction);
        }
        
        protected bool IsValidDirection(Vector2 direction)
        {
            var characterDirection = CharacterDirectionHelper.GetDirectionByVector(direction);
            
            return CharacterAllowedDirections.Contains(characterDirection);
        }

        private Vector2 GetTargetDirection()
        {
            switch (Character.State.DirectionState)
            {
                case CharacterDirection.Down: return Vector2.down;
                case CharacterDirection.Up: return Vector2.up;
                case CharacterDirection.Left: return Vector2.left;
                case CharacterDirection.Right: return Vector2.right;
                default: return Vector2.zero;
            }
        }


        #endregion

        #region Collision Handlers

        private bool IsCollidingWalls(Vector2 direction)
        {
            var origin = (Vector2)transform.position + direction * OriginRayDistance;

            var minWallDistance = 0.1F;

            var hit = Physics2D.Raycast(origin, direction * DistanceView, ObstaclesLayer);
            
            Debug.DrawRay(origin, direction * DistanceView);

            if (hit.transform == null) return false;
            if (!hit.transform.CompareTag(GameConstants.WALL_TAG)) return false;
            
            return (hit.distance <= minWallDistance);
        }
        
        private bool CanWalkThrough(Vector2 originPosition, Vector2 direction)
        {
            var origin = (Vector2)transform.position + originPosition * OriginRayDistance;

            var hit = Physics2D.Raycast(origin, direction * DistanceView, ObstaclesLayer);
            
            Debug.DrawRay(origin, direction * DistanceView);

            if (hit.transform == null) return false;

            return !hit.collider.CompareTag(GameConstants.WALL_TAG);
        }

        private bool CanWalkThrough(Vector2 direction)
        {
            var validDirections = new List<Vector2>();
            
            for (int i = 0; i < RaysDirections.Count; i++)
            {
                var rays = RaysDirections[i];

                var origin = (Vector2)transform.position + rays * OriginRayDistance;
                
                var characterDirection = CharacterDirectionHelper.GetDirectionByVector(direction);

                var hit = Physics2D.Raycast(origin, direction * DistanceView);
                
                if(hit.collider != null)
                    if(!hit.collider.CompareTag(GameConstants.WALL_TAG))
                        if (validDirections.Exists(e => e == direction))
                            validDirections.Add(direction);
                            
            }

            return false;
        }
        
        private void Vision()
        {
            if(Time.time - _lastUpdateTime <= 2.0F) return;

            _lastUpdateTime = Time.time;
            
            for (int i = 0; i < VectorDirectionList.Count; i++)
            {
                for (int j = 0; j < RaysDirections.Count; j++)
                {
                    var currentDirection = VectorDirectionList[i];
                    var currentRayDirection = RaysDirections[j];                
                    var characterDirection = CharacterDirectionHelper.GetDirectionByVector(currentDirection);
                    
                    if (CanWalkThrough(currentRayDirection, currentDirection))
                    {
                        Debug.Log("<color=green>VALID DIRECTION: " + characterDirection + "</color>");
                        if (!CharacterAllowedDirections.Exists(e => e == characterDirection))
                            CharacterAllowedDirections.Add(characterDirection);
                    }
                    else
                    {
                        CharacterAllowedDirections.RemoveAll(e => e == characterDirection);
                        Debug.Log("<color=red>INVALID DIRECTION: " + characterDirection + "</color>");
                    }
                }
            }
        }

        private void CheckWalls()
        {
            for (int i = 0; i < VectorDirectionList.Count; i++)
            {
                var currentDirection = VectorDirectionList[i];

                var rayOrigin = (Vector2)transform.position + currentDirection * OriginRayDistance;

                var hit = Physics2D.Raycast(rayOrigin, currentDirection * DistanceView, ObstaclesLayer);
                
                if(hit.transform != null)
                    if (!hit.collider.CompareTag(GameConstants.WALL_TAG))
                        return;
                    else
                    {
                        var distance = Vector3.Distance(transform.position, hit.transform.position);

                        //Debug.Log(distance + hit.collider.tag + currentDirection);

                        Animator.speed = distance <= 0.15F ? 0 : 1F;
                
                        Debug.DrawRay(rayOrigin, currentDirection * DistanceView);
                    }         
            }
        }     
        
        #endregion

        #region Directions

        private void RestoreStartDirections()
        {
            return;
            CharacterAllowedDirections.Clear();
            
            CharacterAllowedDirections.Add(CharacterDirection.Left);
            CharacterAllowedDirections.Add(CharacterDirection.Right);
            CharacterAllowedDirections.Add(CharacterDirection.Null);
        }
        
        private void PopulateDirections()
        {
            foreach (CharacterDirection direction in Enum.GetValues(typeof(CharacterDirection)))
            {
                if(direction == CharacterDirection.Null) continue;
                CharacterAllowedDirections.Add(direction);
            }
        }

        #endregion

        #region Methods

        public void AuthorizingWalkAfterTime()
        {
            if(Character.State.ConditionState == CharacterCondition.Freeze)
                Invoke("AuthorizingWalk", _timeToStartMove);    
        }

        private void StoreInitialPosition()
        {
            _startPosition = transform.position;
        }

        private void RestoreInitialPosition()
        {
            transform.position = _startPosition;
        }     

        private void EnableCollider()
        {
            var collider = GetComponent<Collider2D>();

            collider.enabled = true;
        }

        private void EnableInput()
        {
            Character.LinkedInputController.AuthorizingMove = true;
        }

        public async void Respawn()
        {            
            FixPosition();
            
           // EnableCollider();        
            
            Character.State.ChangeConditionState(CharacterCondition.Freeze);
            Character.State.ChangeDirectionState(CharacterDirection.Left);

            await Task.Delay(TimeSpan.FromSeconds(_timeToStartMove));
            
            AuthorizingWalk();
        }
        
        public void AuthorizingWalk()
        {
            EnableInput();
            RestoreStartDirections();
            
            Character.State.ChangeConditionState(CharacterCondition.Normal);
        }

        #endregion
        
        private void OnLevelStart()
        {
            AuthorizingWalkAfterTime();
        }                               
    }
}