using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
public class BoardManager : MonoBehaviour
{
    [SerializeField] private Transform borderParent;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] [Range(5, 16)] private int rows = 8;
    [SerializeField] [Range(5, 16)] private int columns = 8;
    [SerializeField] [Min(35)] private int pieceSize = 35;
    [SerializeField] private float spacing = 1f;
    [SerializeField] private float widthDiff;
    [SerializeField] private float heightDiff;
    [SerializeField] private List<Piece> spawnedPieces = new List<Piece>();
    [SerializeField] private List<GameObject> spawnedBorder = new List<GameObject>();
    [SerializeField] private List<Piece> matchedPieces = new List<Piece>();
    private List<Vector2Int> searchedPos = new List<Vector2Int>();

    private Piece[,] pieces;
    private RectTransform _rectTransform;
    private Vector3 center;
    private UnityAction OnDetectSpecialPiece;
    private bool discoUnlocked = false;
    private int specialPieceColorIndex = -1;
    private Vector2Int specialPiecePos;
    private bool bombUnlocked = false;
    List<Piece> tempColumnMatches = new List<Piece>();
    List<Piece> tempRowMatches = new List<Piece>();
    

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyUp(KeyCode.Space))
            GenerateRandomizeBoard();
#endif
    }

    public void SetBoardSize(int column,int row)
    {
        columns = Mathf.Clamp(column,GameManager.Instance.gameTweak.MinBoardNum,GameManager.Instance.gameTweak.MaxBoardNum);
        rows = Mathf.Clamp(row,GameManager.Instance.gameTweak.MinBoardNum,GameManager.Instance.gameTweak.MaxBoardNum);
    }

    private void CheckReference()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
    }

    public void GenerateRandomizeBoard()
    {
        columns = Random.Range(5, 16);
        rows = Random.Range(5, 16);
        GenerateBoard();
    }

    public void GenerateBoard()
    {
        DOTween.KillAll();
        CheckReference();
        pieces = new Piece[columns, rows];
        GameManager.Instance.ColorPool.Clear();
        SetupColorPoolFromBoardSize();
        ClearBoard();
        CalculatePieceSize();
        PopulateBoard();
    }

    private void SetupColorPoolFromBoardSize()
    {
        if (columns * rows <= 64)
        {
            foreach (var color in GameManager.Instance.gameTweak.levelOne)
                GameManager.Instance.ColorPool.Add(color);
        }
        else if (columns * rows >= 65 && columns * rows <= 100)
        {
            foreach (var color in GameManager.Instance.gameTweak.levelOne)
                GameManager.Instance.ColorPool.Add(color);

            foreach (var color in GameManager.Instance.gameTweak.levelTwo)
                GameManager.Instance.ColorPool.Add(color);
        }
        else
        {
            foreach (var color in GameManager.Instance.gameTweak.levelOne)
                GameManager.Instance.ColorPool.Add(color);

            foreach (var color in GameManager.Instance.gameTweak.levelTwo)
                GameManager.Instance.ColorPool.Add(color);

            foreach (var color in GameManager.Instance.gameTweak.levelThree)
                GameManager.Instance.ColorPool.Add(color);
        }
    }

    private void CalculatePieceSize()
    {
        var cellWidth = _rectTransform.rect.width / columns - ((spacing / columns) * 2);
        var cellHeight = _rectTransform.rect.height / rows - ((spacing / rows) * 2);

        if (cellWidth < cellHeight)
            pieceSize = Mathf.FloorToInt(cellWidth);
        else
            pieceSize = Mathf.FloorToInt(cellHeight);

        var totalColumnAvailable = Mathf.FloorToInt(_rectTransform.rect.width / pieceSize);
        var totalRowsAvailable = Mathf.FloorToInt(_rectTransform.rect.height / pieceSize);

        heightDiff = 0;
        heightDiff = totalRowsAvailable - rows;

        widthDiff = 0;
        widthDiff = totalColumnAvailable - columns;
    }

    private void PopulateBoard()
    {
        positionOffset = Vector3.zero;
        center = Vector3.zero;
        spawnedPieces.Clear();

        if (heightDiff > 0)
        {
            center.y = heightDiff * pieceSize / 2.0f;
            center.y = center.y + ((pieceSize - spacing) * heightDiff) / 2.0f;
        }

        if (widthDiff > 0)
            center.x = widthDiff * pieceSize / 2.0f;

        positionOffset = _rectTransform.position - center;


        for (int column = 0; column < columns; column++)
        for (int row = 0; row < rows; row++)
        {
            Piece piece = GameManager.Instance.Pool.PickFromPool(Globals.PoolTag.piece).GetComponent<Piece>();
            if (piece == null)
            {
                Debug.LogError($"tried to get obj from pool with null Piece class!".InColor(Color.red), gameObject);
                break;
            }

            spawnedPieces.Add(piece);
            if (piece.transform.parent != transform)
                piece.transform.SetParent(transform);

            var posX = (pieceSize * column);
            var posY = (pieceSize * row);
            SetupPieceTransform(piece, posX, posY, column, row);
            SpawnBorder(posX, posY);
        }
    }

    private void SetupPieceTransform(Piece piece, int posX, int posY, int column, int row)
    {
        piece.transform.localPosition = new Vector3(posX, posY + _rectTransform.rect.height, 0) - positionOffset;
        piece.transform.localScale = new Vector3(pieceSize - spacing, pieceSize - spacing, 1);
        piece.gameObject.SetActive(true);
        piece.SetupPieceData(new Vector2Int(column, row), new Vector3(posX, posY, 0) - positionOffset);
        pieces[column, row] = piece;
    }

    private void SpawnBorder(int posX, int posY)
    {
        GameObject border = GameManager.Instance.Pool.PickFromPool(Globals.PoolTag.border);
        border.transform.SetParent(borderParent);
        border.transform.localPosition = new Vector3(posX, posY, 0) - positionOffset;
        border.transform.localScale = new Vector3(pieceSize - spacing, pieceSize, 1);
        border.gameObject.SetActive(true);
        spawnedBorder.Add(border);
    }

    public void CheckMatchesFromPiece(Piece targetPiece)
    {
        searchedPos.Clear();
        matchedPieces.Clear();
        OnDetectSpecialPiece = null;

        matchedPieces = GetMatchList(targetPiece);
        for (int p = 0; p < matchedPieces.Count; p++)
        {
            var temp = GetMatchList(matchedPieces[p]);
            if (temp == null)
                continue;
            // while (temp.Count >)
            {
                for (int j = 0; j < temp.Count; j++)
                {
                    if (matchedPieces.Contains(temp[j]) == false)
                        matchedPieces.Add(temp[j]);
                }    
            }
        }

        if (matchedPieces.Count > 1)
        {
            if (matchedPieces.Count >= 6 && matchedPieces.Count < 10)
            {
                bombUnlocked = true;
                discoUnlocked = false;
                SetupSpecialPieceData(targetPiece);
            }
            
            if (matchedPieces.Count >= 10)
            {
                discoUnlocked = true;
                bombUnlocked = false;
                SetupSpecialPieceData(targetPiece);
            }

            for (int p = 0; p < matchedPieces.Count; p++)
            {
                matchedPieces[p].OnSelected();
                if (matchedPieces[p] is BaseSpecialPiece)
                {
                    var piece = matchedPieces[p];
                    OnDetectSpecialPiece = () =>
                    {
                        GameManager.Instance.Score.SetPlayerScore(matchedPieces.Count);
                        piece.OnClickPiece();
                    };
                    
                }
            }

            if (OnDetectSpecialPiece == null)
            {
                GameManager.Instance.Score.SetPlayerScore(matchedPieces.Count);
                GameManager.Instance.Board.FillEmptyPositions();
            }
            else
                OnDetectSpecialPiece.Invoke();    
          
        }
    }

    private void SetupSpecialPieceData(Piece targetPiece)
    {
        specialPieceColorIndex = targetPiece.ColorIndex;
        specialPiecePos = targetPiece.Position;
    }
    
    private void OnSpawnSpecialPiece(BaseSpecialPiece newPiece, int column, int row)
    {
        var tempX = (pieceSize * specialPiecePos.x);
        var tempY = (pieceSize * specialPiecePos.y);
        newPiece.customColorIndex = specialPieceColorIndex;
        SetupPieceTransform(newPiece, tempX, tempY, specialPiecePos.x, specialPiecePos.y);
        pieces[column, row] = newPiece;
    }

    public bool CheckColorMatch(int colorIndex)
    {
        var score = 0;
        OnDetectSpecialPiece = null;
        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (pieces[c, r].ColorIndex == colorIndex)
                {
                    if (pieces[c, r].IsSelected == false && pieces[c,r].gameObject.activeInHierarchy)
                    {
                        score += 1;
                        pieces[c, r].OnSelected();    
                    }
                    
                }
            }
        }

        if (score > 1)
        {
            OnDetectSpecialPiece?.Invoke();
            GameManager.Instance.Timer.AddTimeLimit(GameManager.Instance.gameTweak.discoPieceTimeBonus,colorIndex);
            GameManager.Instance.Score.SetPlayerScore(Mathf.FloorToInt(matchedPieces.Count * GameManager.Instance.gameTweak.discoPieceBonus));
            GameManager.Instance.Board.FillEmptyPositions();
        }

        return score > 1;
    }
    
    public bool DestroySameDimension(Vector2Int targetPos)
    {
        var score = 0;
        OnDetectSpecialPiece = null;
        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (c == targetPos.x || r == targetPos.y)
                {
                    
                    if (pieces[c, r].IsSelected == false && pieces[c, r].gameObject.activeInHierarchy)
                    {
                        score += 1;
                        pieces[c, r].OnSelected();
                    }
                        
                }
            }
        }

        if (score > 1)
        {
            OnDetectSpecialPiece?.Invoke();
            GameManager.Instance.Timer.AddTimeLimit(GameManager.Instance.gameTweak.bombPieceTimeBonus);
            GameManager.Instance.Score.SetPlayerScore(Mathf.FloorToInt(matchedPieces.Count * GameManager.Instance.gameTweak.bombPieceBonus));
            GameManager.Instance.Board.FillEmptyPositions();
        }

        return score > 1;
    }

    public bool CheckMatchPossibility()
    {
        HashSet<Piece> matchedPieces = new HashSet<Piece>();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                matchedPieces.UnionWith(FindColumnMatchFromPiece(pieces[column, row]));
                matchedPieces.UnionWith(FindRowMatchFromPiece(pieces[column, row]));
            }
        }

        return matchedPieces.Count > 0;
    }

    private void FillEmptyPositions()
    {
        for (int column = 0; column < columns; column++)
        for (int row = 0; row < rows; row++)
        {
            while (pieces[column, row].IsSelected || pieces[column,row].gameObject.activeInHierarchy == false)
            {
                Piece current = pieces[column, row];
                Piece next = current;
                Vector2Int tempPos = next.Position;

                for (int filler = row; filler < rows - 1; filler++)
                {
                    next = pieces[column, filler + 1];
                    current = next;
                    tempPos = next.Position;
                    var posX = (pieceSize * column);
                    var posY = (pieceSize * filler);

                    //Move down upper piece to fill downward
                    current.OverwritePos(new Vector2Int(column, filler));
                    current.MoveToTargetPos(new Vector3(posX, posY, 0) - positionOffset);

                    //Update piece array
                    pieces[column, filler] = current;
                }

                //Fill up empty position on board
                var newPiece = GameManager.Instance.Pool.PickFromPool(Globals.PoolTag.piece).GetComponent<Piece>();
                var tempX = (pieceSize * tempPos.x);
                var tempY = (pieceSize * tempPos.y);
                SetupPieceTransform(newPiece, tempX, tempY, tempPos.x, tempPos.y);
            }

            if (bombUnlocked)
            {
                if (pieces[column, row].Position == specialPiecePos)
                {
                    var temp = pieces[column, row];
                    temp.OnSelected();
                    var newPiece = GameManager.Instance.Pool.PickFromPool(Globals.PoolTag.bomb).GetComponent<BaseSpecialPiece>();
                    OnSpawnSpecialPiece(newPiece, column, row);
                    bombUnlocked = false;
                }
            }

            if (discoUnlocked)
            {
                if (pieces[column, row].Position == specialPiecePos)
                {
                    var temp = pieces[column, row];
                    temp.OnSelected();
                    var newPiece = GameManager.Instance.Pool.PickFromPool(Globals.PoolTag.disco).GetComponent<BaseSpecialPiece>();
                    OnSpawnSpecialPiece(newPiece, column, row);
                    discoUnlocked = false;
                }
            }
        }

        if (CheckMatchPossibility() == false)
        {
            GameManager.Instance.State.GetStateViaType(typeof(GameState)).EndState();
            GameManager.Instance.State.GetStateViaType(typeof(GameOverState)).StartState();
        }

        spawnedPieces.Clear();
        foreach (var piece in pieces)
            spawnedPieces.Add(piece);
    }

    private List<Piece> GetMatchList(Piece targetPiece)
    {
        //Detect if the piece has already been searched
        if (searchedPos.Contains(targetPiece.Position))
            return null;
        searchedPos.Add(targetPiece.Position);
        List<Piece> tempMatches = new List<Piece>();
        var horizontalMatches = FindColumnMatchFromPiece(targetPiece);
        var verticalmatches = FindRowMatchFromPiece(targetPiece);

        if (horizontalMatches.Count >= 1)
        {
            for (int i = 0; i < horizontalMatches.Count; i++)
            {
                if (tempMatches.Contains(horizontalMatches[i]) == false)
                    tempMatches.Add(horizontalMatches[i]);              
            }
        }
        
        
        if (verticalmatches.Count >= 1)
            for (int j = 0; j < verticalmatches.Count; j++)
            {
                if (tempMatches.Contains(verticalmatches[j]) == false)
                    tempMatches.Add(verticalmatches[j]);                      
            }

        return tempMatches;
    }
    
    private List<Piece> FindColumnMatchFromPiece(Piece targetPiece)
    {
        tempColumnMatches.Clear();
        Piece nextPiece = targetPiece;
        for (int i = targetPiece.Position.x; i < columns; i++)
        {
            nextPiece = pieces[i, targetPiece.Position.y];
            if (targetPiece.PieceColor == nextPiece.PieceColor)
            {
                if (matchedPieces.Contains(nextPiece) == false)
                    tempColumnMatches.Add(nextPiece);
            }
            else break;
        }

        for (int i = targetPiece.Position.x; i >= 0; i--)
        {
            nextPiece = pieces[i, targetPiece.Position.y];
            if (targetPiece.PieceColor == nextPiece.PieceColor)
            {
                if (matchedPieces.Contains(nextPiece) == false)
                    tempColumnMatches.Add(nextPiece);
            }
            else break;
        }

        return tempColumnMatches;
    }
    
    private List<Piece> FindRowMatchFromPiece(Piece targetPiece)
    {
        tempRowMatches.Clear();
        Piece nextPiece = targetPiece;
        for (int i = targetPiece.Position.y; i < rows; i++)
        {
            nextPiece = pieces[targetPiece.Position.x, i];
            if (targetPiece.PieceColor == nextPiece.PieceColor)
            {
                if (matchedPieces.Contains(nextPiece) == false)
                    tempRowMatches.Add(nextPiece);
            }
            else break;
        }
        
        for (int i = targetPiece.Position.y; i >= 0; i--)
        {
            nextPiece = pieces[targetPiece.Position.x, i];
            if (targetPiece.PieceColor == nextPiece.PieceColor)
            {
                if (matchedPieces.Contains(nextPiece) == false)
                    tempRowMatches.Add(nextPiece);
            }
            else break;
        }

        return tempRowMatches;
    }

    public void ClearBoard()
    {
        ClearSpawnedPieces();
        ClearSpawnedBorders();
    }

    private void ClearSpawnedBorders()
    {
        spawnedBorder.Clear();
        for (int i = 0; i < borderParent.childCount; i++)
            borderParent.GetChild(i).gameObject.SetActive(false);
    }

    private void ClearSpawnedPieces()
    {
        spawnedPieces.Clear();
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(false);
    }
}