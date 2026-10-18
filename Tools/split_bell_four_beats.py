"""Losslessly split the supplied PCM bells on their embedded tempo grid."""
import csv
import hashlib
import io
import re
import wave
from pathlib import Path

SOURCE = Path('C:/Users/96963/Desktop/TapTap/Audio/Lv.1&2')
DEST = Path(__file__).resolve().parents[1] / 'Assets/Audio/Lv.1&2/Bell_4BeatSegments'

def main():
    rows = []
    for variant in range(1, 4):
        source = SOURCE / f'Lv.1 Bell_{variant}.wav'
        original = source.read_bytes()
        tempos = set(re.findall(rb'Tempo: ([0-9.]+)', original))
        assert tempos == {b'100.0'}, (source, tempos)
        with wave.open(io.BytesIO(original), 'rb') as wav:
            params = wav.getparams()
            pcm = wav.readframes(params.nframes)
        assert params.comptype == 'NONE'
        frames_per_part = round(params.framerate * 60 / 100 * 4)
        frame_bytes = params.nchannels * params.sampwidth
        padding_frames = (-params.nframes) % frames_per_part
        padded_pcm = pcm + bytes(padding_frames * frame_bytes)
        folder = DEST / f'Bell_{variant}'
        folder.mkdir(parents=True, exist_ok=True)
        rebuilt = bytearray()
        for index, start in enumerate(range(0, params.nframes + padding_frames, frames_per_part), 1):
            part = padded_pcm[start * frame_bytes:(start + frames_per_part) * frame_bytes]
            output = folder / f'Lv1_Bell_{variant}_Bar_{index:02d}.wav'
            if not output.exists():
                with wave.open(str(output), 'wb') as wav:
                    wav.setparams(params)
                    wav.writeframes(part)
            with wave.open(str(output), 'rb') as wav:
                assert wav.getnframes() == frames_per_part
                assert (wav.getframerate(), wav.getnchannels(), wav.getsampwidth()) == (44100, 2, 3)
                verified = wav.readframes(wav.getnframes())
                assert verified == part
                rebuilt.extend(verified)
            rows.append([source.name, str(output.relative_to(DEST)), index,
                         (index - 1) * 4, start, frames_per_part,
                         start / params.framerate, frames_per_part / params.framerate,
                         hashlib.sha256(part).hexdigest()])
        assert bytes(rebuilt) == padded_pcm, 'Concatenation did not reproduce original audio plus padding'
        assert source.read_bytes() == original, 'Source file changed'
        print(f'{source.name}: {params.nframes / params.framerate:.3f}s, 100 BPM, '
              f'{(params.nframes + padding_frames) // frames_per_part} parts; '
              f'padding={padding_frames} frames ({padding_frames / params.framerate:.6f}s); PCM reconstruction PASS')
    with (DEST / 'segments.csv').open('w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(['source', 'segment', 'bar', 'start_beat_zero_based', 'start_frame',
                         'frames', 'start_seconds', 'duration_seconds', 'pcm_sha256'])
        writer.writerows(rows)
    (DEST / 'README.txt').write_text(
        '100 BPM; four beats per segment; 2.4 seconds / 105840 frames each.\n'
        '44.1 kHz stereo 24-bit PCM. Source audio starts at beat zero.\n'
        'Exact sample cuts: no normalization, fades, resampling or added tails.\n'
        'Concatenating all eight segments reproduces original PCM, followed only by any padding.\n'
        'Bell_3 source is 18.57707483s: Bar_08 has 27471 zero frames (0.62292517s) appended.\n'
        'Individual playback may expose cut reverb tails; audition before gameplay integration.\n'
        'Bell_1, Bell_2, Bell_3 are separate variants; Bar_01..08 are ordered segments within each.\n',
        encoding='utf-8')
    print(f'Wrote {len(rows)} verified segments to {DEST}')

if __name__ == '__main__':
    main()
