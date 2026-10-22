using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField, DisallowMultipleComponent]
    public class CharacterControl_Cubey : MonoBehaviour
    {
        [BigHeader("Control Settings")]
        [Tooltip("The Behavior controls the game/level state, to send the current level state to it")]
        [ReadOnly] public GridLevelData LevelSettings;

        [InnerHint("(Optional)"), Tooltip("UI Text used to display the current remain text")]
        public Text BlockRemainsText;
        [InnerHint("(Optional)"), Tooltip("in this behavior, a progress bar controller attached")]
        public GameObject ProgressBar;
        public int ProgressBarZeroPoint;

        [BigHeader("Move Settings")]
        [Tooltip("Character movement Speed")]
        [Min(.1f)] public float Speed;
        [Tooltip("Touch move strength to enable swipe")]
        [Min(1f)] public float SwipeSinstivity;
        [Tooltip("block box (no step at)")] public LayerMask BlockLayer;
        [Tooltip("Stop moving above it")] public LayerMask CheckPointLayer;

        [BigHeader("Events")]
        public UnityEvent OnTriggerBlock;
        public UnityEvent OnStop;

        [BigHeader("Animations")]
        [Tooltip("The list of all texture animations for all states\ncheck the sorting in ChangeEmoji()")]
        public GameObject[] EmojiList; // 0 for idle // 1 for Moving Right // 2 for Left // 3 for Up // 4 For Down // 5 For Win // 6 for Lose

        [BigHeader("Collection")]
        [Tooltip("Seconds after stopping before checking for a dead end (visited tiles need time to rise).")]
        [Min(0.1f)] public float StuckCheckDelay = 0.6f;
        [Tooltip("Lose when no swipe can move the character any more.")]
        public bool LoseWhenStuck = true;
        [InnerHint("(Optional)"), Tooltip("Marker shown on the cell the hint points to. A tinted cube is created when empty.")]
        public GameObject HintMarker;
        public Color HintColor = new(1f, 0.85f, 0.2f);
        [Tooltip("Tutorial: levels up to this number show the hint for free after every stop. 0 disables.")]
        [Min(0)] public int FreeHintsUpToLevel = 1;
        [Tooltip("Invoked when a hint cannot find a solution from the current position (suggest a restart).")]
        public UnityEvent OnHintUnavailable;
        [InnerHint("(Optional)"), Tooltip("Lose screen text that explains why the level failed.")]
        public Text LoseReasonText;
        public string StuckMessage = "No way to finish from here!";
        [Tooltip("Splat rules: visited tiles are only painted and stay walkable (hints and skips use the Splat solver).")]
        public bool PaintedTilesStayOpen;
        [Tooltip("Prefix of the rewarded ad placements (<prefix>_hint, <prefix>_skip).")]
        public string AdPlacementPrefix = "cubey";

        List<GameObject> _activeBlocks = new();
        Vector3 _nextPosition;
        string _direction; bool _move, _gameOver;
        int _remainBlocks;
        float _progBarStep;

        AllMightyOne.Cubey.CubeyBoard _board;
        AllMightyOne.Splat.SplatBoard _splat;
        bool[] _visited;
        GameObject _hintInstance;
        Coroutine _stuckRoutine;

        void Start()
        {
            LevelSettings = FindObjectOfType<GridLevelData>();
            transform.position = LevelSettings.StartCellObject.transform.position;
            _remainBlocks = LevelSettings.NumberOfEmptyCells + LevelSettings.NumberCountedObjects;
            if (BlockRemainsText) BlockRemainsText.text = _remainBlocks.ToString();
            _progBarStep = (float)ProgressBarZeroPoint / Mathf.Max(1, _remainBlocks);
            _board = AllMightyOne.Cubey.CubeyLevelReader.Read(LevelSettings);
            if (PaintedTilesStayOpen) _splat = AllMightyOne.Splat.SplatBoard.From(_board);
            _visited = new bool[_board.CellCount];
            var gm = FindObjectOfType<UniversalGameManager>();
            _tutorial = gm && gm.CurrentLevel <= FreeHintsUpToLevel;
            if (_tutorial) ShowHint();
        }

        bool _tutorial;

        void Update()
        {
            if (_move)
            {
                // Cap the step below one cell: tiles are counted by trigger overlaps, so a long frame must not jump over a tile
                float step = Mathf.Min(Speed * Time.deltaTime, 0.4f * LevelSettings.CellWorldSize);
                transform.position = Vector3.MoveTowards(transform.position, _nextPosition, step);
                if (Vector3.Distance(transform.position, _nextPosition) < 0.05f) // reach the next point // Stop Character
                {
                    transform.position = _nextPosition;
                    _move = false;
                    if (!_gameOver)
                    {
                        ChangeEmoji("Idle");
                        OnStop?.Invoke();
                        if (LoseWhenStuck)
                        {
                            if (_stuckRoutine != null) StopCoroutine(_stuckRoutine);
                            _stuckRoutine = StartCoroutine(CheckStuck());
                        }
                        if (_tutorial) ShowHint();
                    }
                }
            }
        }

        public void InputDetection(Vector2 direction)
        {
            if (_move || _gameOver) return;
            HideHint();
            TryMove(direction);
        }

        #region Collection: stuck detection, hint, skip
        System.Collections.IEnumerator CheckStuck()
        {
            yield return new WaitForSeconds(StuckCheckDelay);
            _stuckRoutine = null;
            if (_move || _gameOver) yield break;
            foreach (var d in AllMightyOne.Cubey.CubeyBoard.Directions)
                if (CanMovePhysics(AllMightyOne.Cubey.CubeyBoard.GridToSwipe(d))) yield break;
            LoseStuck(); // no swipe moves the character: dead end
        }

        void LoseStuck()
        {
            if (LoseReasonText) LoseReasonText.text = StuckMessage;
            GameOver();
        }

        /// <summary> Hint button: plays a rewarded ad, then shows the hint. A dead position ends the level instead (no ad). </summary>
        public void RequestHint()
        {
            if (_move || _gameOver || _board == null) return;
            var pos = AllMightyOne.Cubey.CubeyLevelReader.WorldToCell(LevelSettings, transform.position);
            if (FirstSolutionStep(pos) == null)
            {
                OnHintUnavailable?.Invoke();
                if (_splat == null && _board.LastSearchExhausted) LoseStuck();
                return;
            }
            AllMightyOne.AdsBridge.ShowRewarded($"{AdPlacementPrefix}_hint", ok => { if (ok && this) ShowHint(); });
        }

        /// <summary> Skip button: plays a rewarded ad, then completes the level. </summary>
        public void RequestSkip()
        {
            if (_gameOver) return;
            AllMightyOne.AdsBridge.ShowRewarded($"{AdPlacementPrefix}_skip", ok => { if (ok && this) SkipLevel(); });
        }

        // Same ray test as TryMove(), without moving
        bool CanMovePhysics(Vector2 direc)
        {
            Vector3 offset = new(direc.x * LevelSettings.CellWorldSize, 0, direc.y * LevelSettings.CellWorldSize);
            if (!Physics.Raycast(new Ray(transform.position, offset.normalized), out RaycastHit hit, 30, BlockLayer + CheckPointLayer)) return false;
            Vector3 next;
            if (BlockLayer == (BlockLayer | (1 << hit.collider.gameObject.layer)))
            {
                Vector3 pt = hit.point - transform.position;
                if (offset.x != 0) pt.x -= pt.x % offset.x;
                if (offset.z != 0) pt.z -= pt.z % offset.z;
                next = transform.position + pt;
            }
            else
            {
                next = hit.collider.gameObject.transform.position;
                next.y = transform.position.y;
            }
            return (next - transform.position).sqrMagnitude > 0.01f;
        }

        /// <summary> Swipe directions that solve the level from the current state (null when it cannot be won). Used by hints and tests. </summary>
        public Vector2[] SolveFromHere()
        {
            if (_board == null) return null;
            var pos = AllMightyOne.Cubey.CubeyLevelReader.WorldToCell(LevelSettings, transform.position);
            var sol = _splat != null ? _splat.Solve(pos, _visited) : _board.Solve(pos, _visited, _remainBlocks);
            return sol?.ConvertAll(AllMightyOne.Cubey.CubeyBoard.GridToSwipe).ToArray();
        }

        public bool IsMoving => _move;
        public bool IsGameOver => _gameOver;

        /// <summary> Shows where the next correct swipe leads (call from a rewarded ad). </summary>
        public void ShowHint()
        {
            if (_move || _gameOver || _board == null) return;
            var pos = AllMightyOne.Cubey.CubeyLevelReader.WorldToCell(LevelSettings, transform.position);
            var step = FirstSolutionStep(pos);
            if (step == null) { OnHintUnavailable?.Invoke(); return; }
            Vector2Int targetCell = _splat != null ? _splat.Slide(pos, step.Value)[^1] : _board.Slide(pos, step.Value, _visited, _remainBlocks).Target;
            Vector3 target = AllMightyOne.Cubey.CubeyLevelReader.CellToWorld(LevelSettings, targetCell);
            target.y = transform.position.y;

            if (!_hintInstance)
            {
                if (HintMarker) _hintInstance = Instantiate(HintMarker);
                else
                {
                    _hintInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(_hintInstance.GetComponent<Collider>());
                    _hintInstance.transform.localScale = new Vector3(0.6f, 0.08f, 0.6f) * LevelSettings.CellWorldSize;
                    var source = GetComponentInChildren<Renderer>();
                    var r = _hintInstance.GetComponent<Renderer>();
                    if (source) r.material = new Material(source.sharedMaterial);
                    r.material.color = HintColor;
                }
                _hintInstance.name = "Cubey Hint";
                _hintInstance.AddComponent<AllMightyOne.Bobbing>();
            }
            _hintInstance.transform.position = target;
            _hintInstance.SetActive(true);
        }

        // first swipe of a solution from the current state (null when the level cannot be finished from here)
        Vector2Int? FirstSolutionStep(Vector2Int pos)
        {
            if (_splat != null)
            {
                var moves = _splat.NextPaintingMoves(pos, _visited); // Splat levels are trap-free: the greedy step is always right
                return moves == null || moves.Count == 0 ? null : moves[0];
            }
            var solution = _board.Solve(pos, _visited, _remainBlocks);
            return solution == null || solution.Count == 0 ? null : solution[0];
        }

        void HideHint()
        {
            if (_hintInstance) _hintInstance.SetActive(false);
        }

        /// <summary> Completes the level without playing it (call from a rewarded ad). </summary>
        public void SkipLevel()
        {
            if (_gameOver) return;
            _gameOver = true;
            _move = false;
            HideHint();
            ChangeEmoji("Win");
            FindObjectOfType<UniversalGameManager>().OnWin();
        }

        void MarkVisited(Vector3 worldPos)
        {
            if (_board == null) return;
            var c = AllMightyOne.Cubey.CubeyLevelReader.WorldToCell(LevelSettings, worldPos);
            if (_board.InBounds(c)) _visited[_board.Index(c)] = true;
        }
        #endregion

        void TryMove(Vector2 direc)
        {
            Vector3 X_Z_Offset = new(direc.x * LevelSettings.CellWorldSize, 0, direc.y * LevelSettings.CellWorldSize); // next target step
            if (X_Z_Offset.magnitude == 0) return; // no valid direction, in case Vec3.zero
            _direction = direc.x == 1 ? "right" : direc.x == -1 ? "left" : direc.y == 1 ? "up" : "down";

            Ray ray = new(transform.position, X_Z_Offset.normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, 30, layerMask: BlockLayer + CheckPointLayer))
            {
                if (BlockLayer == (BlockLayer | (1 << hit.collider.gameObject.layer)))
                {
                    Vector3 pt = hit.point - transform.position;
                    if (X_Z_Offset.x != 0) pt.x -= pt.x % X_Z_Offset.x;
                    if (X_Z_Offset.z != 0) pt.z -= pt.z % X_Z_Offset.z;
                    _nextPosition = transform.position + pt;
                }
                else if (CheckPointLayer == (CheckPointLayer | (1 << hit.collider.gameObject.layer)))
                {
                    _nextPosition = hit.collider.gameObject.transform.position;
                    _nextPosition.y = transform.position.y;
                }
                if (_nextPosition != transform.position)
                {
                    _move = true;
                    ChangeEmoji("Moving");
                }
            }
        }

        public void BlockCreated() // this called from the block object when it's triggerd by character
        {
            _remainBlocks--;
            if (BlockRemainsText) BlockRemainsText.text = _remainBlocks.ToString();
            if (ProgressBar) ProgressBar.transform.localPosition += Vector3.right * _progBarStep;
            //LevelSettings.PercentageCompleted = 100 * _remainBlocks / (_activeBlocks.Count + _remainBlocks);
            OnTriggerBlock?.Invoke();
            if (_remainBlocks == 0 && !_gameOver) GameOver();
        }

        public void GameOver() // When game over (Reach end point)
        {
            if (_gameOver) return;
            _gameOver = true;
            HideHint();
            if (_remainBlocks <= 0) // Win
            {
                ChangeEmoji("Win");
                FindObjectOfType<UniversalGameManager>().OnWin();
            }
            else // Lose
            {
                FindObjectOfType<UniversalGameManager>().OnLose();
                ChangeEmoji("Lost");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Score Object"))
            {
                if (_activeBlocks.Contains(other.gameObject)) return;
                if (other.gameObject == LevelSettings.EndCellObject)
                {
                    transform.position = other.gameObject.transform.position; // because when end block triggerd the movement system disabled
                    MarkVisited(other.transform.position);
                    BlockCreated();
                    if (!_gameOver) GameOver();
                    _activeBlocks.Add(other.gameObject);
                }
                else if (other.gameObject != LevelSettings.StartCellObject)
                {
                    MarkVisited(other.transform.position);
                    BlockCreated();
                }
            }
        }
        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Score Object")) // when character leave the square... activate the block and move it
            {
                if (_activeBlocks.Contains(other.gameObject)) return;
                _activeBlocks.Add(other.gameObject);
            }
        }

        void ChangeEmoji(string state)
        {
            foreach (GameObject em in EmojiList) em.SetActive(false);
            if (state == "Moving") EmojiList[_direction == "right" ? 1 : _direction == "left" ? 2 : _direction == "up" ? 3 : 4].SetActive(true);
            else if (state == "Win") EmojiList[5].SetActive(true);
            else if (state == "Lost") EmojiList[6].SetActive(true);
            else EmojiList[0].SetActive(true);
        }
    }
}
