namespace FinkFramework.Runtime.Input
{
    /// <summary>输入绑定发生冲突时的默认处理策略。</summary>
    public enum InputConflictMode
    {
        /// <summary>允许绑定继续生效，但通过绑定信息和改键结果标记冲突。</summary>
        Warning,

        /// <summary>拒绝冲突绑定，不修改目标绑定。</summary>
        Strict
    }
}
