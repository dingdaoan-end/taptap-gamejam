"""Keep only the first 32 beats, in four lossless eight-beat segments."""
import io
import re
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('C:/Users/96963/Desktop/TapTap/Audio/Lv.1&2/Lv.1 Chords.wav')
DEST = ROOT / 'Assets/Audio/Lv.1&2/Chords_8BeatSegments'

def main():
    original = SOURCE.read_bytes()
    assert set(re.findall(rb'Tempo: ([0-9.]+)', original)) == {b'100.0'}
    with wave.open(io.BytesIO(original), 'rb') as wav:
        params = wav.getparams()
        assert (params.framerate, params.nchannels, params.sampwidth, params.comptype) == (44100, 2, 3, 'NONE')
        frames = 211680
        assert params.nframes >= frames * 4
        pcm = wav.readframes(frames * 4)
    DEST.mkdir(parents=True, exist_ok=True)
    rebuilt = bytearray()
    for i in range(4):
        part = pcm[i * frames * 6:(i + 1) * frames * 6]
        output = DEST / f'Lv1_Chords_Phrase_{i + 1:02d}.wav'
        if not output.exists():
            with wave.open(str(output), 'wb') as wav:
                wav.setparams(params)
                wav.writeframes(part)
        with wave.open(str(output), 'rb') as wav:
            assert wav.getnframes() == frames
            assert (wav.getframerate(), wav.getnchannels(), wav.getsampwidth()) == (44100, 2, 3)
            verified = wav.readframes(frames)
            assert verified == part
            rebuilt.extend(verified)
    assert bytes(rebuilt) == pcm
    assert SOURCE.read_bytes() == original
    (DEST / 'README.txt').write_text(
        '100 BPM. Four eight-beat phrases: 4.8s each, 19.2s / 32 beats total.\n'
        'Source ranges: 0-4.8, 4.8-9.6, 9.6-14.4, 14.4-19.2 seconds.\n'
        '44.1kHz stereo 24-bit PCM; exact sample cuts, no fades, silence padding or resampling.\n'
        'Later repetitions omitted per musical design. Original file untouched.\n', encoding='utf-8')
    print('PASS: 4 x 8 beats; 4.8s each; concatenation equals first 19.2s of original PCM.')

if __name__ == '__main__':
    main()
