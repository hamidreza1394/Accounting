const form=document.querySelector("#connection-form");
const testButton=document.querySelector("#test-button");
const saveButton=document.querySelector("#save-button");
const result=document.querySelector("#result");
const sourceNotice=document.querySelector("#source-notice");
const savedSection=document.querySelector("#saved-section");
const deleteButton=document.querySelector("#delete-button");
let requestToken="";
let testedSignature="";
let canSave=false;

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
      ?"اتصال از متغیر محیطی می‌آید؛ تغییر آن باید در تنظیمات اجرای Visual Studio انجام شود."
      :"اتصال از تنظیمات برنامه می‌آید؛ این صفحه برای جلوگیری از نادیده‌گرفته‌شدن مقدار ذخیره، ذخیره یا حذف را غیرفعال کرده است.";
    saveButton.disabled=true;
    deleteButton.disabled=true;
  }
}
function signature() {
  const data=new FormData(form);
  return JSON.stringify([...data.entries()].map(([key,value])=>[key,key==="password"?"provided":value]));
}
function payload() {
  const data=new FormData(form);
  return {
    server:data.get("server"),database:data.get("database"),
    authentication:data.get("authentication"),username:data.get("username"),
    password:data.get("password"),trustServerCertificate:data.has("trustServerCertificate")
  };
}
document.querySelectorAll('input[name="authentication"]').forEach(input=>input.addEventListener("change",()=>{
  document.querySelector("#sql-credentials").hidden=document.querySelector('input[name="authentication"]:checked').value!=="sql";
  saveButton.disabled=true;testedSignature="";
}));
form.addEventListener("input",()=>{saveButton.disabled=true;testedSignature="";});
testButton.addEventListener("click",async()=>{
  result.className="result";result.textContent="در حال آزمون اتصال…";
  saveButton.disabled=true;testedSignature="";
  const {response,body}=await api("/api/development/database-setup/test",{
    method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken},body:JSON.stringify(payload())
  });
  result.textContent=body.message||"آزمون اتصال انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  if(response.ok){testedSignature=signature();saveButton.disabled=!canSave;}
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
