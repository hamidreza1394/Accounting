const form=document.querySelector("#connection-form");
const testButton=document.querySelector("#test-button");
const inspectButton=document.querySelector("#inspect-button");
const provisionButton=document.querySelector("#provision-button");
const saveButton=document.querySelector("#save-button");
const result=document.querySelector("#result");
const sourceNotice=document.querySelector("#source-notice");
const savedSection=document.querySelector("#saved-section");
const deleteButton=document.querySelector("#delete-button");
const stageList=document.querySelector("#stage-list");
const stagesNote=document.querySelector("#stages-note");
const runtimeSecret=document.querySelector("#runtime-secret");
const runtimeSecretBody=document.querySelector("#runtime-secret-body");
let requestToken="";
let testedSignature="";
let canSave=false;

const STATE_LABELS={pending:"در انتظار",running:"در حال اجرا",success:"موفق",skipped:"بی‌تغییر",failed:"ناموفق"};
const STAGE_TITLES={
  connection:"اعتبارسنجی اطلاعات اتصال",
  test:"آزمون اتصال به سرور",
  inspect:"بازرسی وضعیت سرور و دیتابیس",
  createDatabase:"ایجاد دیتابیس Vendor در صورت نیاز",
  migrate:"اعمال Migrationهای EF Core",
  runtimeIdentity:"ایجاد هویت Runtime کم‌اختیار",
  verify:"بررسی آمادگی با اتصال Runtime"
};

async function api(path,options={}) {
  const response=await fetch(path,{credentials:"same-origin",cache:"no-store",...options});
  const body=await response.json().catch(()=>({}));
  return {response,body};
}
async function loadPageState() {
  const token=await api("/api/development/database-setup/antiforgery");
  requestToken=token.body.requestToken||"";
  const {body}=await api("/api/development/database-setup/status");
  canSave=body.canSave===true;
  savedSection.hidden=body.savedInWindowsCredentialManager!==true;
  if(!canSave) {
    sourceNotice.hidden=false;
    sourceNotice.textContent=body.source==="environment"
      ?"اتصال از متغیر محیطی می‌آید؛ تغییر آن باید در تنظیمات اجرای Visual Studio انجام شود. راه‌اندازی کامل اجرا می‌شود اما چیزی ذخیره نمی‌کند."
      :"اتصال از تنظیمات برنامه می‌آید؛ این صفحه برای جلوگیری از نادیده‌گرفته‌شدن مقدار ذخیره، ذخیره یا حذف را غیرفعال کرده است. راه‌اندازی کامل اجرا می‌شود اما چیزی ذخیره نمی‌کند.";
    saveButton.disabled=true;
    deleteButton.disabled=true;
  }
}
function signature() {
  const data=new FormData(form);
  return JSON.stringify([...data.entries()].map(([key,value])=>[key,key==="password"||key==="runtimePassword"?"provided":value]));
}
function payload() {
  const data=new FormData(form);
  return {
    server:data.get("server"),database:data.get("database"),
    authentication:data.get("authentication"),username:data.get("username"),
    password:data.get("password"),trustServerCertificate:data.has("trustServerCertificate"),
    runtimeUsername:data.get("runtimeUsername"),runtimePassword:data.get("runtimePassword")
  };
}
function setBusy(busy) {
  testButton.disabled=busy;
  inspectButton.disabled=busy;
  provisionButton.disabled=busy;
  if(busy)saveButton.disabled=true;
}
function renderStages(stages) {
  stageList.hidden=false;
  stagesNote.hidden=false;
  stageList.replaceChildren(...stages.map(stage=>{
    const item=document.createElement("li");
    item.className="stage "+(STATE_LABELS[stage.state]?stage.state:"pending");
    const state=document.createElement("span");
    state.className="state";
    state.textContent=STATE_LABELS[stage.state]||stage.state;
    const title=document.createElement("span");
    title.className="title";
    title.textContent=STAGE_TITLES[stage.key]||stage.key;
    const message=document.createElement("span");
    message.className="message";
    message.textContent=stage.message||"";
    item.append(state,title);
    if(stage.message)item.append(message);
    return item;
  }));
}
document.querySelectorAll('input[name="authentication"]').forEach(input=>input.addEventListener("change",()=>{
  document.querySelector("#sql-credentials").hidden=document.querySelector('input[name="authentication"]:checked').value!=="sql";
  saveButton.disabled=true;testedSignature="";
}));
form.addEventListener("input",()=>{saveButton.disabled=true;testedSignature="";});
testButton.addEventListener("click",async()=>{
  result.className="result";result.textContent="در حال آزمون اتصال…";
  setBusy(true);testedSignature="";
  const {response,body}=await api("/api/development/database-setup/test",{
    method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken},body:JSON.stringify(payload())
  });
  result.textContent=body.message||"آزمون اتصال انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  setBusy(false);
  if(response.ok)saveButton.disabled=!canSave;
});
inspectButton.addEventListener("click",async()=>{
  result.className="result";result.textContent="در حال بررسی وضعیت…";
  runtimeSecret.hidden=true;
  setBusy(true);
  const {response,body}=await api("/api/development/database-setup/inspect",{
    method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken},body:JSON.stringify(payload())
  });
  result.textContent=body.message||"بررسی وضعیت انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  renderStages(body.stages||[]);
  setBusy(false);
});
provisionButton.addEventListener("click",async()=>{
  result.className="result";result.textContent="راه‌اندازی کامل در حال اجراست؛ این عملیات چند دقیقه ممکن است طول بکشد و نباید صفحه بسته شود…";
  runtimeSecret.hidden=true;
  setBusy(true);
  // One-time bootstrap token paired with a strict cookie: fetched fresh for every attempt
  // and consumed by the server on use, so a replayed request cannot run provisioning again.
  const bootstrap=await api("/api/development/database-setup/provision-token");
  const bootstrapToken=bootstrap.body.token||"";
  if(!bootstrapToken){
    result.textContent="دریافت نشست راه‌اندازی ناموفق بود؛ صفحه را دوباره باز کنید و یک‌بار دیگر تلاش کنید.";
    result.className="result error";
    setBusy(false);
    return;
  }
  const {response,body}=await api("/api/development/database-setup/provision",{
    method:"POST",
    headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken,"X-Faranou-Setup-Token":bootstrapToken},
    body:JSON.stringify(payload())
  });
  result.textContent=body.message||"راه‌اندازی کامل انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  renderStages(body.stages||[]);
  if(body.runtimeUsername){
    runtimeSecretBody.textContent="نام کاربری Runtime: "+body.runtimeUsername+(body.generatedPassword?" — گذرواژهٔ ساخته‌شده: "+body.generatedPassword:" — گذرواژهٔ فرم استفاده شد.");
    runtimeSecret.hidden=false;
  }
  setBusy(false);
  testedSignature="";
  if(response.ok)await loadPageState();
});
form.addEventListener("submit",async event=>{
  event.preventDefault();
  if(!testedSignature||testedSignature!==signature()||!canSave)return;
  saveButton.disabled=true;
  result.className="result";result.textContent="اتصال دوباره بررسی و ذخیره می‌شود…";
  const {response,body}=await api("/api/development/database-setup/save",{
    method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken},body:JSON.stringify(payload())
  });
  result.textContent=body.message||"ذخیره انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  if(response.ok){testedSignature="";savedSection.hidden=false;}
});
deleteButton.addEventListener("click",async()=>{
  if(!confirm("تنظیم اتصال ذخیره‌شده برای این کاربر ویندوز حذف شود؟"))return;
  const {response,body}=await api("/api/development/database-setup/saved",{
    method:"DELETE",headers:{"X-CSRF-TOKEN":requestToken}
  });
  result.textContent=body.message||"حذف انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  if(response.ok)savedSection.hidden=true;
});
loadPageState();
