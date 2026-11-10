using System;
using System.Linq;
using System.Threading;
using DefaultNamespace;
using Unity.Mathematics;
using UnityEngine;

public class ObjectScatterer : MonoBehaviour
{
    [SerializeField] private ObjectScattererConfiguration _config;

    [SerializeField] private Transform _scatterPoint;
    [SerializeField] private float     _scatterRange;
    [SerializeField] private int       _scatterCount;
    [SerializeField] private float     _scatterDelay;

    private CancellationTokenSource _cancellationTokenSource;

    [ContextMenu("Scatter")]
    private async Awaitable Scatter()
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            Application.exitCancellationToken,
            destroyCancellationToken);
        
        var rnd      = new Unity.Mathematics.Random();
        var objects  = new GameObject[_scatterCount];
        var maxCount = _config.Prefabs.Count;

        for (int index = 0; index < _scatterCount; index++)
        {
            var seed = (uint)(DateTime.Now.Ticks + index);
            
            rnd.InitState(seed);

            var configIndex      = rnd.NextInt(0, maxCount);
            var scatterDirection = (Vector3)rnd.NextFloat3Direction() * _scatterRange;
            
            var configPrefab = _config.Prefabs[configIndex];

            var output = Core.Instantiate(configPrefab, _scatterPoint.position + scatterDirection, quaternion.identity);

            await Awaitable.WaitForSecondsAsync(_scatterDelay, _cancellationTokenSource.Token);
            
            objects[index] = output;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_scatterPoint is null || 
            _config is null ||
            math.length(_scatterRange) == 0.0f ||
            _scatterCount == 0)
            return;
        
        Gizmos.color = Color.purple;
        Gizmos.DrawWireSphere(_scatterPoint.position, _scatterRange);
    }

    private void OnGUI()
    {
        var buttonWidth  = Screen.width - 20;
        var buttonHeight = 60;
        
        {
            var rect = new Rect(10, Screen.height - buttonHeight - 10, buttonWidth, buttonHeight);
            var style = new GUIStyle(GUI.skin.button);

            style.fontSize = 30;

            if (GUI.Button(rect, "Scatter", style))
            {
                _ = Scatter();
            }
        }
    }
}
