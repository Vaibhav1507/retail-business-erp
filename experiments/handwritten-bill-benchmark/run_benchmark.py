#!/usr/bin/env python3
import argparse, base64, csv, json, mimetypes, os, re, time
from datetime import datetime, timezone
from pathlib import Path
from anthropic import Anthropic
from google import genai
from google.genai import types
from PIL import Image
SAMPLES=[f'S{i:02d}' for i in range(1,12)]
MODELS=[('anthropic','claude-haiku-5-5'),('gemini','gemini-3.8-flash')]
PROMPT='''Transcribe this handwritten Indian retail SALES BILL. Return only JSON with invoice_number, invoice_date, items (description_verbatim, model_or_identifier_verbatim, quantity, unit_rate, line_amount, discount, tax, uncertainty_note), subtotal, printed_grand_total, currency (INR), document_uncertainty_notes. Preserve verbatim handwriting and row order. Use null for blank or unreadable values; never infer or calculate blank fields. Keep boxed/printed TOTAL separate from annotations elsewhere. Do not extract customer name, phone, address, GSTIN, signature or any personal data. Do not invent rows or values.'''
def image_bytes(path):
    mime=mimetypes.guess_type(path.name)[0] or 'image/jpeg'; data=path.read_bytes()
    if mime=='image/webp':
        import io
        with Image.open(path) as im: b=io.BytesIO(); im.convert('RGB').save(b,format='JPEG',quality=95); data=b.getvalue()
        mime='image/jpeg'
    return mime,data
def call(provider,model,path):
    mime,data=image_bytes(path); start=time.perf_counter(); rid=None; usage={'input_tokens':0,'output_tokens':0}
    if provider=='anthropic':
        c=Anthropic(api_key=os.environ['ANTHROPIC_API_KEY'],max_retries=0,timeout=90)
        try:
            r=c.messages.create(model=model,max_tokens=4096,system='Careful document transcription. JSON only.',messages=[{'role':'user','content':[{'type':'image','source':{'type':'base64','media_type':mime,'data':base64.b64encode(data).decode()}},{'type':'text','text':PROMPT}]}])
            text=''.join(x.text for x in r.content if getattr(x,'type',None)=='text'); usage={'input_tokens':r.usage.input_tokens,'output_tokens':r.usage.output_tokens}; rid=getattr(r,'_request_id',None)
        finally: c.close()
    else:
        c=genai.Client(api_key=os.environ['GEMINI_API_KEY'],http_options=types.HttpOptions(timeout=90000))
        r=c.models.generate_content(model=model,contents=[types.Part.from_bytes(data=data,mime_type=mime),PROMPT],config=types.GenerateContentConfig(response_mime_type='application/json',temperature=0,max_output_tokens=4096))
        text=r.text or ''; u=getattr(r,'usage_metadata',None)
        if u: usage={'input_tokens':getattr(u,'prompt_token_count',0) or 0,'output_tokens':getattr(u,'candidates_token_count',0) or 0}
        rid=getattr(r,'response_id',None)
    m=re.search(r'\{.*\}',text,re.S)
    if not m: raise ValueError('Provider returned no JSON object')
    return json.loads(m.group()),usage,round((time.perf_counter()-start)*1000),rid
def main():
    p=argparse.ArgumentParser(); p.add_argument('--images',type=Path,required=True); p.add_argument('--output',type=Path,required=True); a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=True); images={}
    for s in SAMPLES:
        found=list(a.images.glob(s+'.*'))
        if len(found)!=1: raise SystemExit(f'{s}: expected one image, found {len(found)}')
        images[s]=found[0]
    for k in ['ANTHROPIC_API_KEY','GEMINI_API_KEY']:
        if not os.getenv(k): raise SystemExit(f'Missing required secret: {k}')
    results=[]
    # Deliberately sequential: all 11 Haiku requests finish before Gemini starts.
    for provider,model in MODELS:
        for sample,path in images.items():
            row={'sample_id':sample,'provider':provider,'model':model,'status':'failed'}
            try:
                data,usage,ms,rid=call(provider,model,path); row.update(status='success',extraction=data,usage=usage,latency_ms=ms,request_id=rid)
            except Exception as e: row.update(error_type=type(e).__name__,http_status=getattr(e,'status_code',None),error_message=str(e)[:250])
            results.append(row); (a.output/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2))
            print(f"{provider} {sample}: {row['status']} {row.get('latency_ms','')}ms")
    lines=['# Handwritten sales-bill benchmark','',f"Run: {os.getenv('BENCHMARK_LABEL','unlabelled')} · {datetime.now(timezone.utc).isoformat()}",'','11 bills × 2 models. Haiku first, then Gemini. Gold labels remain provisional; this run does not claim verified accuracy.','','| Model | Success | Failed | Mean latency (s) | Token cost estimate (USD) |','|---|---:|---:|---:|---:|']
    prices={'claude-haiku-5-5':(0.10,0.50),'gemini-3.8-flash':(0.75,3.75)}
    for _,model in MODELS:
        subset=[r for r in results if r['model']==model]; ok=[r for r in subset if r['status']=='success']; lat=[r['latency_ms'] for r in ok]; pi,po=prices[model]
        cost=sum((r['usage']['input_tokens']*pi+r['usage']['output_tokens']*po)/1e6 for r in ok)
        lines.append(f"| {model} | {len(ok)} | {len(subset)-len(ok)} | {sum(lat)/len(lat)/1000:.2f} | {cost:.6f} |" if lat else f'| {model} | 0 | {len(subset)} | n/a | 0 |')
    lines += ['','## Per-sample outputs','']
    for s in SAMPLES:
        lines.append(f'### {s}')
        for _,model in MODELS:
            r=next(x for x in results if x['sample_id']==s and x['model']==model)
            if r['status']!='success': lines.append(f"- {model}: FAILED ({r.get('error_type')}, HTTP {r.get('http_status')})"); continue
            d=r['extraction']; items=d.get('items',[]); lines.append(f"- {model}: invoice {d.get('invoice_number') or '—'}; date {d.get('invoice_date') or '—'}; rows {len(items)}; printed total {d.get('printed_grand_total')}; {r['latency_ms']} ms")
            for i,x in enumerate(items,1): lines.append(f"  - {i}. {x.get('description_verbatim') or '—'} | qty {x.get('quantity')} | rate {x.get('unit_rate')} | amount {x.get('line_amount')}")
        lines.append('')
    (a.output/'summary.md').write_text('\n'.join(lines),encoding='utf-8')
    with (a.output/'metrics.csv').open('w',newline='',encoding='utf-8') as f:
        fields=['sample_id','provider','model','status','latency_ms','input_tokens','output_tokens','http_status','error_type']; w=csv.DictWriter(f,fieldnames=fields); w.writeheader()
        for r in results: w.writerow({**{k:r.get(k) for k in fields},'input_tokens':r.get('usage',{}).get('input_tokens'),'output_tokens':r.get('usage',{}).get('output_tokens')})
if __name__=='__main__': main()
