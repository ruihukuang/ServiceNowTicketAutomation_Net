# ServiceNowTicketAutomation_Net

Project explanation

This project is to automate the process of providing insights for ServiceNow incident tickets for a certain period. The insights could show 3 aspects below:   
1 Whether SLA is met and What are average extra days to fix issues if SLA is met  
2 Whether tickets are assigned to right responsible team, the responsible team fix the issues and whether extra team are included to affect metting SLA.    
3 What are persistent problems and root causes in which platforms.  
4 Identify duplicate tickets in groups.    

Fake data are created for this project but they are based on actual cases in industry. 

The front end link is https://github.com/ruihukuang/ServiceNowTicketAutomation_React.

Design  

<img width="1295" height="719" alt="image" src="https://github.com/user-attachments/assets/f55fc5b9-09f6-4bbb-8fa1-76c240a1f2ff" />



Local Test Results:

Check Health of the app: 

![Alt text](/app_pics/healthy_app.png)

API call to query data from database 

![Alt text](/app_pics/API_call_query_data_from_DB.png)

Locally run docker container for Ollama. Ollama object Hermes-3-Llama-3.1-8B.Q4_K_M.gguf is downloaded from Hugging face 

![Alt text](/app_pics/local_run_ollama_container.png)

API call to send data to locally run docker container Ollam to process data to identify what system/platform is broken bassed on ServiceNow Data

![Alt text](/app_pics/API_call_to_local_run_Ollama_process_data.png)
