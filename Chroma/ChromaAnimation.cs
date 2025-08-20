// Ruta: SimpleExtractor/Chroma/ChromaAnimation.cs
namespace Chroma
{
    public class ChromaAnimation
    {
        public SortedDictionary<int, ChromaFrame> States;

        public ChromaAnimation()
        {
            States = new SortedDictionary<int, ChromaFrame>();
        }
    }
}