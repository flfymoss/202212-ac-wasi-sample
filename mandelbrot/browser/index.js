import { init, WASI } from '@wasmer/wasi';

function draw(N, ctx, source) {
  console.log('Drawing...');

  const bPerRow = Math.ceil(N / 8.0);

  for (let i = 0; i < N; ++i)
  {
      let ri = 0;
      for (let j = 0; j < bPerRow; ++j)
      {
          let pByte = source[i * bPerRow + j];
          for (let k = 0; k < 8; ++k)
          {
              if (ri == N) break;
              if (((pByte << k) & 0x80) == 0x80) ctx.fillRect((j * 8 + k), i, 1, 1);
            
              ri++;
          }
      }
  }

  console.log('Done.')
}

async function generate(module, N) {
  let wasi = new WASI({
    env: {
        // 'ENVVAR1': '1',
        // 'ENVVAR2': '2'
    },
    args: [
      '', `${N}`
    ],
  });
  
  let importObject = wasi.getImports(module);
  let instance = await WebAssembly.instantiate(module, importObject);
  
  // Run the start function
  let exitCode = wasi.start(instance);
  console.log(`exitcode : ${exitCode}`);
  return wasi.getStdoutBuffer();
}

async function main() {
  // This is needed to load the WASI library first (since is a Wasm module)
  await init();

  const moduleBytes = fetch("./mandelbrot.wasm");
  const module = await WebAssembly.compileStreaming(moduleBytes);

  const submitButton = document.getElementById('buttons__submit--button');
  const nOption = document.getElementById('buttons__N--select');
  const elapsedTime = document.getElementById('elapsed_time');
  const elapsedTimeDefaultText = elapsedTime.textContent;
  submitButton.addEventListener('click', async () => {
    submitButton.disabled = true;
    // For UI update
    await new Promise(r => setTimeout(r, 50));

    const startDate = new Date();
    const N = nOption.value;
  
    const output = await generate(module, N);
    console.log('Generated.');
  
    const canvas = document.getElementById('pbm_view');
    canvas.width = N;
    canvas.height = N;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = 'rgb(0,0,0)';
  
    const headerByteLen = (new TextEncoder()).encode(`P4\n${N} ${N}\n`).byteLength;
    draw(N, ctx, output.slice(headerByteLen));
    const endDate = new Date();

    elapsedTime.textContent = `${elapsedTimeDefaultText} ${endDate - startDate} ms`
    submitButton.disabled = false;
  });
}

document.addEventListener('DOMContentLoaded', () => main());
