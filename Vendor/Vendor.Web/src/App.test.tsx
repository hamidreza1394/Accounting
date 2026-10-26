import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import App from './App';
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
describe('Vendor control plane shell',()=>{
 it('shows the Persian sign-in screen when no session exists',async()=>{vi.stubGlobal('fetch',vi.fn().mockResolvedValue(new Response('{}',{status:401,headers:{'Content-Type':'application/json'}})));render(<App/>);expect(await screen.findByRole('heading',{name:'به پنل مرکزی وارد شوید'})).toBeInTheDocument();expect(screen.getByLabelText('نام کاربری یا ایمیل')).toBeRequired();});
 it('moves to TOTP only after the real password endpoint requests a second factor',async()=>{const f=vi.fn().mockResolvedValueOnce(new Response('{}',{status:401})).mockResolvedValueOnce(new Response(JSON.stringify({requestToken:'test-token'}),{status:200,headers:{'Content-Type':'application/json'}})).mockResolvedValueOnce(new Response(JSON.stringify({requiresSecondFactor:true,method:'totp'}),{status:200,headers:{'Content-Type':'application/json'}}));vi.stubGlobal('fetch',f);render(<App/>);fireEvent.change(await screen.findByLabelText('نام کاربری یا ایمیل'),{target:{value:'vendor-admin'}});fireEvent.change(screen.getByLabelText('رمز عبور'),{target:{value:'not-a-real-password'}});fireEvent.click(screen.getByRole('button',{name:/ادامه و تأیید هویت/}));expect(await screen.findByLabelText('کد احراز هویت')).toBeInTheDocument();expect(f).toHaveBeenCalledWith('/api/auth/password',expect.objectContaining({method:'POST'}));});
});
