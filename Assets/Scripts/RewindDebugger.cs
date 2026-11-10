using UnityEngine;

namespace DefaultNamespace
{
    public class RewindDebugger : MonoBehaviour
    {
        private void OnGUI()
        {
            const int buttonWidth  = 400;
            const int buttonHeight = 60;
            const int spacing      = 10;

            var style = new GUIStyle(GUI.skin.button) { fontSize = 30 };

            if (DrawButton(0, "Clear Rewind Data"))
                RewindSystem.Clear();

            if (DrawButton(1, "Start Rewind (Mono)"))
                RewindSystem.Rewind(RewindMode.Mono);

            if (DrawButton(2, "Start Rewind (Job)"))
                RewindSystem.Rewind(RewindMode.Job);

            bool DrawButton(int row, string label)
            {
                var rect = new Rect(
                    Screen.width - buttonWidth - spacing,
                    spacing + row * (buttonHeight + spacing),
                    buttonWidth,
                    buttonHeight);

                return GUI.Button(rect, label, style);
            }
        }
    }
}
