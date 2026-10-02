import os, sys
from PIL import Image
D = r'D:\库文件\文档\ChatGPT\MD'
im = Image.open(os.path.join(D, '新补充截图', '结束失败画面.png')).convert('RGB')
crop = im.crop((300, 330, 1700, 900))
crop.save(os.path.join(D, 'probe', 'out2', 'defeat_crop.png'))
print(crop.size)
