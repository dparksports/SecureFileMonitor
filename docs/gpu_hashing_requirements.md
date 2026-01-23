# GPU SHA-256 Implementation Requirements

To transition from the CPU-fallback safety mode to **true Hardware Acceleration** on the GPU, we need to implement the following components using `ComputeSharp`.

## 1. The HLSL Kernel (Shader)
We need a valid HLSL (High-Level Shader Language) implementation of the SHA-256 algorithm. This runs on the GPU cores.

### Components Needed:
- **Constants**: The 64 SHA-256 constants (`K` values).
- **Bitwise Operations**: `ROTR` (Right Rotate), `ShR` (Shift Right), `Ch` (Choose), `Maj` (Majority), `Sigma0`, `Sigma1`.
- **Message Schedule**: Logic to expand the 16-word block into the 64-word schedule.
- **Compression Loop**: The main loop that updates the hash state (a, b, c, d, e, f, g, h).

### Challenge: Serial vs Parallel
- **Single File Hashing**: SHA-256 is inherently serial. Block 2 requires the resulting hash of Block 1.
  - *Implication*: You cannot easily parallelize a single file's hash on a GPU. A single GPU thread is slower than a single CPU thread (latency).
  - *Performance*: True GPU acceleration for a single file is often **slower** due to PCIe transfer overhead.
  - *Recommendation*: Use GPU only for **Merkle Tree / Block Hashing**.

- **Block Hashing (Merkle Tree)**: This is "Embarrassingly Parallel".
  - We have 1,000 blocks of 1MB each.
  - We can launch 1,000 GPU threads.
  - Each thread computes the SHA-256 of its own independent block.
  - **This is where the GPU shines.**

## 2. Memory Management (Host <-> Device)
We need to efficiently move data to VRAM.

- **UploadBuffer**: We need to chunk the file (e.g., 64MB chunks) and upload them to the GPU.
- **Pinned Memory**: For maximum speed, we should use Pinned Host Memory to allow DMA (Direct Memory Access) transfers to the GPU.

## 3. Implementation Steps (`GpuHasherService`)

```csharp
// Pseudo-code for ComputeBlockHashesAsync
public async Task<string[]> ComputeBlockHashesAsync(string filePath)
{
    // 1. Read File into Byte Array
    byte[] data = File.ReadAllBytes(filePath);

    // 2. Allocate GPU Memory
    using ReadOnlyBuffer<byte> gpuBuffer = GraphicsDevice.GetDefault().AllocateReadOnlyBuffer(data);
    using ReadWriteBuffer<uint> resultBuffer = GraphicsDevice.GetDefault().AllocateReadWriteBuffer<uint>(blockCount * 8);

    // 3. Dispatch Shader
    // One thread per block
    GraphicsDevice.GetDefault().For(blockCount, new Sha256BlockKernel(gpuBuffer, resultBuffer));

    // 4. Read Results
    uint[] results = resultBuffer.ToArray();
    
    // 5. Convert to Strings
    return ConvertToStrings(results);
}
```

## 4. The `Sha256BlockKernel` Struct
This is the complex part. It must be a `readonly partial struct` implementing `IComputeShader`.

```csharp
[AutoConstructor]
public readonly partial struct Sha256BlockKernel : IComputeShader
{
    public readonly ReadOnlyBuffer<byte> Input;
    public readonly ReadWriteBuffer<uint> Output;

    public void Execute()
    {
        // HLSL Code here...
        // 1. Calculate Offset based on ThreadID.x
        // 2. Read 64-byte chunk
        // 3. Run SHA-256 Logic
        // 4. Write 32-byte hash to Output[ThreadID.x]
    }
}
```

## Summary
To "do it right", we need to write the **HLSL SHA-256 Kernel** and focus on **Parallel Block Hashing** rather than single-file hashing.
