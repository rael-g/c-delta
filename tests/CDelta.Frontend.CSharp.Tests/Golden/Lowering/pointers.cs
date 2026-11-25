struct Node
{
    public int Value;
    public int Next;
}

static unsafe class Program
{
    static int Main()
    {
        int a = 1;
        int* p = &a;
        *p = 7;
        Node node;
        node.Value = 5;
        node.Next = 6;
        Node* n = &node;
        n->Value = n->Value + 1;
        (*n).Next += 10;
        int* q = &node.Value;
        int* r = &node.Next;
        long between = r - q;
        bool ordered = q < r && q != r && p != null;
        return a + node.Value + node.Next + (int)between + (ordered ? 100 : 0) + sizeof(Node);
    }
}
