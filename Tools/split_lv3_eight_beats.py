"""Split LV3 melodies on the embedded 116 BPM grid; verify lossless PCM reconstruction."""
import csv
import hashlib
import io
import math
from pathlib import Path
import re
import uuid
import wave

ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / 'Assets/Audio/LV3'
SOURCES = ['LV3_Bell_1', 'LV3_Bell_2', 'LV3_Bell_3',
           'LV3_Duduk_1', 'LV3_Duduk_2', 'LV3_Chords_Section']
BPM = 116


def meta(path, template):
    target = Path(str(path) + '.meta')
    if not target.exists():
        target.write_text(re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, template), encoding='utf-8')


def main():
    for name in SOURCES:
        source = AUDIO / (name + '.wav')
        original = source.read_bytes()
        assert set(re.findall(rb'Tempo: ([0-9.]+)', original)) == {b'116.0'}
        with wave.open(io.BytesIO(original), 'rb') as wav:
            params = wav.getparams()
            pcm = wav.readframes(params.nframes)
        assert (params.framerate, params.nchannels, params.sampwidth, params.comptype) == (44100, 2, 3, 'NONE')
        # Round absolute boundaries, avoiding cumulative sample rounding drift.
        frames_per_segment = params.framerate * 60 * 8 / BPM
        count = math.ceil((params.nframes - 1) / frames_per_segment)
        boundaries = [round(i * frames_per_segment) for i in range(count + 1)]
        padding = boundaries[-1] - params.nframes
        assert padding >= 0
        frame_bytes = params.nchannels * params.sampwidth
        padded = pcm + bytes(padding * frame_bytes)
        folder = AUDIO / (name + '_8BeatSegments')
        folder.mkdir(exist_ok=True)
        meta(folder, 'fileFormatVersion: 2\nguid: 0\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
        importer = Path(str(source) + '.meta').read_text(encoding='utf-8')
        importer = re.sub(r'compressionFormat: \d+', 'compressionFormat: 0', importer)
        importer = re.sub(r'normalize: \d+', 'normalize: 0', importer)
        rebuilt = bytearray()
        rows = []
        for index, (start, end) in enumerate(zip(boundaries, boundaries[1:]), 1):
            part = padded[start * frame_bytes:end * frame_bytes]
            output = folder / f'{name}_8Beats_{index:02d}.wav'
            if not output.exists():
                with wave.open(str(output), 'wb') as wav:
                    wav.setparams(params)
                    wav.writeframes(part)
            with wave.open(str(output), 'rb') as wav:
                assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes()) == (2, 3, 44100, end - start)
                verified = wav.readframes(wav.getnframes())
                assert verified == part, f'Existing output differs: {output}'
            rebuilt.extend(verified)
            meta(output, importer)
            rows.append([output.name, (index - 1) * 8, start, end, end - start,
                         (end - start) / params.framerate, max(0, end - max(start, params.nframes)),
                         hashlib.sha256(part).hexdigest()])
        assert bytes(rebuilt) == padded
        assert source.read_bytes() == original
        with (folder / 'segments.csv').open('w', newline='', encoding='utf-8') as handle:
            writer = csv.writer(handle)
            writer.writerow(['file', 'start_beat', 'start_frame', 'end_frame_exclusive', 'frames', 'seconds', 'padding_frames', 'pcm_sha256'])
            writer.writerows(rows)
        (folder / 'README.txt').write_text(
            f'Source: ../{source.name}\n116 BPM; 8 beats per segment; {count} segments.\n'
            'Cuts start at source time zero. Absolute beat boundaries rounded to nearest sample.\n'
            '44.1 kHz stereo 24-bit PCM; no resampling, normalization or fades.\n'
            f'Final segment padded with {padding} silent frames ({padding / params.framerate:.9f} seconds).\n'
            'Concatenated PCM verified equal to original PCM plus the documented silence.\n'
            f'Source SHA-256: {hashlib.sha256(original).hexdigest()}\n', encoding='utf-8')
        for filename in ['segments.csv', 'README.txt']:
            meta(folder / filename, 'fileFormatVersion: 2\nguid: 0\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
        print(f'{name}: {count} segments; padding {padding} frames ({padding / params.framerate:.6f}s); PCM reconstruction PASS')


if __name__ == '__main__':
    main()
