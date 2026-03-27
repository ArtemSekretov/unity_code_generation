namespace CodeGeneration.Runtime.Visitor
{
    public interface IVisitor
    {
        void Visit<TValue>(string fieldName, ref TValue v);
    }
    
    public delegate void VisitorDelegate<TContainer>(IVisitor visitor, ref TContainer container); 
    
    public static class VisitorCall<TContainer>
    {
        public static VisitorDelegate<TContainer> Visit;
    }
}
