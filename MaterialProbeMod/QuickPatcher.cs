using Harmony;
using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

//A class for inserting patches into IL code. Theoretically, this could be templated to work on anything.
static class QuickPatcher
{
    //Set to a positive number to cause these classes to emit debugging information to the log.
    //1 - Just some information about successful patches
    //2 - More information about comparisons and partial matches
    //3 - Excessive information, shows outputted instructions
    public static int DebugVerbosity { get; set; } = 1;

    public interface IPatcher
    {
        //Called before DoPatch, return the number of instructions you would like in the buffer.
        int GetBufferReserve();
        //Gives the Patcher access to the buffer. You may modify it however you like. Warning: the buffer
        //may not actually contain the number of instructions you requested in GetBufferReserve()! It will
        //contain at least one element. Return true if your patch is considered complete; DoPatch will not
        //be called again.
        //
        //origPos is the index of the instruction as related to the original method. Note that if earlier
        //patches have changed code, this might not be exactly what you expect, but the patching code tries
        //to compensate for additions or subtractions to the buffer.
        //written is the actual number of instructions written so far. If no patches have changed the size
        //of the buffer, it should be equal to origPos.
        //You should use it for comparing
        bool DoPatch(Queue<CodeInstruction> buffer, int origPos, int written);
    }

    //A very simple patcher. It simply looks for a matching set of instructions, and then inserts a patch,
    //either before or after those instructions.
    public class SimpleMatchInsertPatcher : IPatcher
    {
        private readonly CodeInstruction[] match;
        private readonly CodeInstruction[] insert;
        private readonly bool after;

        public CodeInstruction[] Match => match;
        public CodeInstruction[] Insert => insert;
        public bool Before => !after;
        public bool After  =>  after;
        
        //After the patch is applied, this contains the approximate location the patch was inserted, in
        //relation to the original method.
        public int PatchedOriginalLocation { get; private set; }
        //After the patch is applied, this contains the location in the output stream the patch was written to.
        public int PatchedWrittenLocation { get; private set; }

        //Takes the list of instructions to match against, and the instructions to insert around those instructions.
        //You may pass null for the instructions to insert. This is called a null patch, and does nothing, but can be
        //used to search for a certain pattern before moving onto the next patch.
        public SimpleMatchInsertPatcher(CodeInstruction[] match, CodeInstruction[] insert, bool after = true)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            if (insert != null && insert.Length == 0)
                insert = null;
            this.insert = insert;
            this.after = after;
        }
        
        public int GetBufferReserve() { return match.Length; }
        public bool DoPatch(Queue<CodeInstruction> buffer, int origPos, int written)
        {
            if (buffer.Count < match.Length) return false;
            if (QuickPatcher.StartsWith(buffer, match))
            {
                //match!
                if (insert != null)
                {
                    //Debug.Log("Before: \n" + string.Join("\n", new List<string>(buffer.Select(i => i.ToString())).ToArray() ) );
                    if (!after)
                    {
                        //Ugh. C#'s Queue class kinda sucks. 
                        //This is like, the worst way to do this, but Queue's interface is so strict we don't have a choice.
                        CodeInstruction[] orig = buffer.ToArray();

                        buffer.Clear();
                        foreach (var e in insert) buffer.Enqueue(e);
                        foreach (var e in orig) buffer.Enqueue(e);
                    }
                    else
                    {
                        CodeInstruction[] orig = null;

                        //If the buffer is exactly the size of the match, we can cheat, we *only* need to append the patch.
                        if (buffer.Count != match.Length)
                        {
                            //Otherwise, we have to split the Queue to make sure our patch is inserted only after the match.
                            orig = buffer.ToArray();
                            buffer.Clear(); //clear the buffer
                            //Enqueue the match again.
                            for (int i = 0; i < match.Length; i++)
                                buffer.Enqueue(orig[i]);
                        }
                        //Enqueue the patch.
                        foreach (var e in insert) buffer.Enqueue(e);

                        //Append what's left of the buffer if necessary
                        if (orig != null)
                            for (int i = match.Length; i < orig.Length; i++)
                                buffer.Enqueue(orig[i]);
                    }
                    //Debug.Log("After: \n" + string.Join("\n", new List<string>(buffer.Select(i => i.ToString())).ToArray()));

                } //else Null patch
                PatchedOriginalLocation = !after ? origPos : origPos + match.Length;
                PatchedWrittenLocation = !after ? written : written + match.Length;
                if (DebugVerbosity >= 1)
                    Debug.Log(string.Format("PATCH: Inserted patch ({2} instructions) at {0} / {1} ", PatchedOriginalLocation, PatchedWrittenLocation, insert == null ? 0 : insert.Length));
                return true;
            }
            return false;
        }

    }
    public class LabelStealerPatcher : IPatcher
    {
        private readonly CodeInstruction[] match;
        private readonly int markOffset;

        public CodeInstruction Target { get; set; }
        public CodeInstruction[] Match => match;
        public int MarkOffset => markOffset;

        //After the patch is applied, this contains the approximate location the patch was applied.
        public int PatchedOriginalLocation { get; private set; }
        //After the patch is applied, this contains the location in the output stream the patch was applied.
        public int PatchedWrittenLocation { get; private set; }

        //Takes the list of instructions to match against, and which of those instructions to steal the labels from.

        public LabelStealerPatcher(CodeInstruction[] match, int markOffset = 0)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            this.markOffset = markOffset;
            if (markOffset < 0 || markOffset >= match.Length) throw new ArgumentException("markOffset out of range of match");
        }

        public int GetBufferReserve() { return match.Length; }
        public bool DoPatch(Queue<CodeInstruction> buffer, int origPos, int written)
        {
            if (buffer.Count < match.Length) return false;
            if (QuickPatcher.StartsWith(buffer, match))
            {
                //match!
                if (Target == null)
                    throw new Exception("Can't steal labels, target not set!");

                var enumr = buffer.GetEnumerator();
                for (int i = 0; i < markOffset; i++)
                    enumr.MoveNext();
                enumr.MoveNext();
                var mark = enumr.Current;
                if (mark.labels.Count == 0)
                    throw new Exception("Can't steal labels, mark didn't have any labels to steal!");

                foreach (var label in mark.labels)
                    Target.labels.Add(label);

                mark.labels.Clear();

                PatchedOriginalLocation = origPos;
                PatchedWrittenLocation = written;
                if (DebugVerbosity >= 1)
                    Debug.Log(string.Format("PATCH: Stole {6} labels from [{2} {3}], gave them to [{4} {5}] at {0} / {1} ", PatchedOriginalLocation, PatchedWrittenLocation, mark.opcode, mark.operand, Target.opcode, Target.operand, Target.labels.Count));
                return true;
            }
            return false;
        }

    }

    public class LocalReference
    {
        public LocalReference(LocalBuilder lopr) : this(lopr.LocalType, lopr.LocalIndex) { }

        public LocalReference(Type type, int index)
        {
            Type = type;
            Index = index;
        }

        public override bool Equals(Object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var other = (LocalReference)obj;
            return Type == other.Type && Index == other.Index;
        }
        public override string ToString()
        {
            return string.Format("Local {0} ({1})", Index, Type.Name);
        }

        public override int GetHashCode()
        {
            var hashCode = 686506176;
            hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(Type);
            hashCode = hashCode * -1521134295 + Index.GetHashCode();
            return hashCode;
        }

        public Type Type { get; }
        public int Index { get; }
    }

    public static bool InstructionsEqual(CodeInstruction left, CodeInstruction right)
    {
        //if (DebugVerbosity >= 2)
        //    Debug.Log(string.Format("CMP {0} {1} ==? {2} {3}", left.opcode, left.operand, right.opcode, right.operand));
        if (left.opcode != right.opcode) return false;
        object lopr = left.operand;
        object ropr = right.operand;
        if (lopr == null) return lopr == ropr;

        if (lopr is LocalBuilder) lopr = new LocalReference((LocalBuilder)lopr);
        if (ropr is LocalBuilder) ropr = new LocalReference((LocalBuilder)ropr);
        if (!lopr.Equals(ropr)) return false;

        return true;
    }
    public static bool StartsWith(IEnumerable<CodeInstruction> haystack, IEnumerable<CodeInstruction> needle, bool debug = false)
    {
        int matchCount = 0;
        IEnumerator<CodeInstruction> haystackEnum = haystack.GetEnumerator();
        foreach (CodeInstruction left in needle)
        {
            if (!haystackEnum.MoveNext())
                return false; //haystack is too short
            var right = haystackEnum.Current;
            bool equal = InstructionsEqual(left, right);
            if (DebugVerbosity >= 2)
            {
                if (equal)
                    Debug.Log(string.Format("+MATCH [{0} {1}]", left.opcode, left.operand));
                else
                {
                    Debug.Log(string.Format("-COMPR [{0} {1}] != [{2} {3}", left.opcode, left.operand, right.opcode, right.operand));
                    if (matchCount > 0)
                        Debug.Log(string.Format("-MATCH FAILED"));
                }
            }
            if (!equal)
                return false; //haystack doesn't match
            matchCount++;
        }
        if (DebugVerbosity >= 2)
            Debug.Log(string.Format("+MATCH SUCCESS"));
        return true;
    }

    //Applies the given patches, in the order given. The second patch will not be processed until the first is successful,
    //and so on.
    public static IEnumerable<CodeInstruction> ApplyPatches(MethodBase method, IEnumerable<CodeInstruction> instr, IEnumerable<IPatcher> patches)
    {
        Queue<CodeInstruction> buff = new Queue<CodeInstruction>();
        IEnumerator<CodeInstruction> source = instr.GetEnumerator();
        List<IPatcher> lpatches = new List<IPatcher>(patches);
        Dictionary<int, LocalBuilder> capturedLocals = new Dictionary<int, LocalBuilder>();

        int patchNum = 0;

        bool hasNext = true;
        int origPos = 0;
        int written = 0;
        //Iterate as long as the buffer contains something, or could contain something.
        while (buff.Count > 0 || hasNext)
        {
            //Figure out how much we need to reserve
            var patch = patchNum < lpatches.Count ? lpatches[patchNum] : null;
            int read = 0;
            if (hasNext)
            {
                int reserve = 8;
                if (patch != null)
                {
                    reserve = patch.GetBufferReserve();
                    if (reserve < 8)
                        reserve = 8;
                }

                //Bring the buffer up to size.
                int size = buff.Count;
                if (!QueueBuffer(buff, source, reserve))
                    hasNext = false;
                read = buff.Count - size;
            }
            if (patch == null)
            {
                //No more patches, just flush
                while (buff.Count > 0)
                    yield return buff.Dequeue();
                continue;
            }

            //Peek at the buffer, to steal locals from.
            if (read > 0)
            {
                var enumr = buff.GetEnumerator();
                for (int i = 0; i < buff.Count - read; i++)
                    enumr.MoveNext();

                for (int i = 0; i < read; i++)
                {
                    enumr.MoveNext();
                    var inst = enumr.Current;
                    if (inst.operand is LocalBuilder)
                    {
                        var local = (LocalBuilder)inst.operand;
                        if (DebugVerbosity >= 2 && !capturedLocals.ContainsKey(local.LocalIndex))
                            Debug.Log("CAPTURE: " + local);
                        capturedLocals[local.LocalIndex] = local;
                    }
                }
            }

            //Call the patch
            if (patch != null)
            {
                int diff = buff.Count;
                if (patch.DoPatch(buff, origPos, written))
                {
                    if (DebugVerbosity >= 1)
                        Debug.Log(string.Format("Patcher {0}: Applied starting at {1} (Method: {2}.{3})", patchNum, origPos, method.DeclaringType.FullName, method.Name));
                    patchNum++;
                }
                diff = buff.Count - diff; //How much larger or smaller the buffer is now.

                //If the buffer is longer now, we need to reduce origPos to compensate.
                //If it's shorter, the opposite needs to occur
                origPos -= diff;
                
                if (DebugVerbosity >= 2 && patchNum >= lpatches.Count)
                    Debug.Log("PATCH: All patches applied, flushing...");

            }

            if (buff.Count > 0)
            {
                origPos++;
                written++;
                var org = buff.Dequeue();
                //var inst = new CodeInstruction(org);
                //inst.labels = new List<Label>(org.labels);
                //inst.blocks = new List<ExceptionBlock>(org.blocks);

                var inst = org; //We can't clone the instruction, because some patchers, like LabelStealer, rely on the actual instance of the instruction,
                //and modify it *after* we return it. Because we can't clone it, it means we have to modify the original.

                var oper = inst.operand;
                if (oper is LocalReference)
                {
                    //Harmony and the ILGenerator won't know what to do with our LocalReference, so convert it to a LocalBuilder.
                    //Unfortunately, Harmony doesn't give us access to these, and there's no way to get them from the ILGenerator,
                    //so we just watch the instruction stream and "capture" them along the way.
                    var localref = (LocalReference)oper;
                    LocalBuilder local;
                    if (!capturedLocals.TryGetValue(localref.Index, out local))
                        throw new Exception(string.Format("Patcher {0}: [{1} {2}]: tried to emit a reference to a local variable we haven't captured! (Method: {3}.{4})", patchNum, inst.opcode, inst.operand, method.DeclaringType.FullName, method.Name));
                    if (local.LocalType != localref.Type)
                        throw new Exception(string.Format("Patcher {0}: [{1} {2}]: tried to emit a reference to a local variable of the wrong type! Expected {3}, captured type was {4} (Method: {5}.{6})", patchNum, inst.opcode, inst.operand, localref.Type, local.LocalType, method.DeclaringType.FullName, method.Name));
                    inst.operand = local;
                }
                if (DebugVerbosity >= 3)
                    Debug.Log(string.Format("OUT [{0} {1}] (a {2})", inst.opcode, inst.operand, inst.operand == null ? "null" : inst.operand.GetType().Name));

                yield return inst;

                //You might think that we can just restore the instruction to it's original state here, but that doesn't work, since Harmony collects
                //all instructions before passing them on. From Harmony's perspective, all the modifications have been undone before it even
                //touches the instructions.
                //
                //yield return can be weird sometimes.
                //
                //So we have no choice (as is) but to modify the instructions we receive. Maybe we'll figure out a way around this in the future.
            }
        }

        if (patchNum < lpatches.Count)
            throw new Exception(string.Format("Patcher {0} (a {1}) failed to apply! (Method: {2}.{3})", patchNum, lpatches[patchNum].GetType().Name, method.DeclaringType.FullName, method.Name));
    }

    //Ensures the buffer has at least targetSize elements. Returns true if this can be satisfied, otherwise
    //returns false. (Because the source is empty.)
    static bool QueueBuffer(Queue<CodeInstruction> buff, IEnumerator<CodeInstruction> source, int targetSize)
    {
        while (buff.Count < targetSize)
        {
            if (!source.MoveNext())
                return false; //Out of elements

            var left = source.Current;
            //Debug.Log(string.Format("read {0} : {1}", left.opcode, left.operand));

            buff.Enqueue(source.Current);
        }
        return true;
    }
}